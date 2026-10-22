# Requires PowerShell 7.4+, a local MongoDB replica set and the solution built into artifacts/checks.
# Uses an isolated database and loopback ports; never uses the application's configured database.
param([string]$BuildRoot = 'artifacts/checks')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$build = Join-Path $root $BuildRoot
$dbName = 'adnoir_session_load_' + [Guid]::NewGuid().ToString('N')
$run = New-Item -ItemType Directory -Path (Join-Path $build $dbName)
$password = 'Aa1!' + [Guid]::NewGuid().ToString('N')
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$mongosh = (Get-Command mongosh -ErrorAction SilentlyContinue).Source
if (-not $mongosh) { $mongosh = Join-Path $env:LOCALAPPDATA 'Programs/mongosh/mongosh.exe' }
if (-not (Test-Path $mongosh)) { throw 'mongosh is required for isolated database cleanup.' }
foreach ($port in @(16380,16382,16390)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port is already in use." }
}
$common = @{
    DOTNET_ENVIRONMENT='Development'; ASPNETCORE_ENVIRONMENT='Development'
    AdNoir__Mongo__ConnectionString='mongodb://localhost:27017/?replicaSet=rs0'
    AdNoir__Mongo__DatabaseName=$dbName
    AdNoir__RabbitMq__Host=''; AdNoir__Redis__ConnectionString=''
    AdNoir__Jwt__Issuer='http://localhost:16380'; AdNoir__Jwt__Audience='adnoir'
    AdNoir__Jwt__JwksUri='http://localhost:16380/.well-known/jwks.json'
    AdNoir__Jwt__PrivateKeyPem=''; AdNoir__Seed__SuperAdminPassword=$password
    AdNoir__Identity__GrpcAddress='http://localhost:16382'
    AdNoir__Gateway__AuthenticationPermitLimit='1000'
}
function Start-App($name, $content, $extra) {
    $settings = $common.Clone()
    foreach ($key in $extra.Keys) { $settings[$key]=$extra[$key] }
    $dll=Join-Path $build "bin/$name/debug/$name.dll"
    $arguments=@('"'+$dll+'"','--contentRoot','"'+(Join-Path $root $content)+'"')
    $logName="$name-$($children.Count)"
    $process=Start-Process dotnet -ArgumentList $arguments -Environment $settings -WorkingDirectory $run.FullName -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run "$logName.out.log") -RedirectStandardError (Join-Path $run "$logName.err.log")
    $children.Add($process)
    return $process
}
function Wait-Http($url) {
    for ($i=0; $i -lt 40; $i++) {
        try { $null=Invoke-WebRequest $url -TimeoutSec 2; return } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "Startup failed: $url"
}
$script:checks = 0
function Check($label,$method,$path,$body,$token,$expected) {
    $headers=@{}
    if ($token) { $headers.Authorization="Bearer $token" }
    $parameters=@{Uri="http://localhost:16380$path"; Method=$method; Headers=$headers; SkipHttpErrorCheck=$true; TimeoutSec=20}
    if ($null -ne $body) { $parameters.Body=($body | ConvertTo-Json -Depth 8 -Compress); $parameters.ContentType='application/json' }
    $response=Invoke-WebRequest @parameters
    # Response bodies may contain tokens; never include them in failures or logs.
    if ([int]$response.StatusCode -ne $expected) { throw "$label expected $expected got $($response.StatusCode)" }
    $script:checks++
    Write-Host "PASS $label ($expected)"
    if ($response.Content) { return ($response.Content | ConvertFrom-Json) }
}
function Login($slug,$email) {
    return (Check "login $slug" POST '/api/auth/login' @{tenantSlug=$slug;email=$email;password=$password} $null 200).data.accessToken
}

$key=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$common['AdNoir__GrpcSecurity__Callers__0__Credential__ServiceId']='sample'
$common['AdNoir__GrpcSecurity__Callers__0__Credential__Key']=$key
$common['AdNoir__GrpcSecurity__Callers__0__CanReadPermissionMatrix']='true'
$common['AdNoir__GrpcSecurity__Callers__0__CanValidateUserSession']='true'
$common['AdNoir__GrpcSecurity__Callers__0__AllTenants']='true'
$common['AdNoir__Identity__ServiceCredentials__sample__ServiceId']='sample'
$common['AdNoir__Identity__ServiceCredentials__sample__Key']=$key
$common['AdNoir__RegistrationNotifications__PublishWelcomeEvent']='false'
$common['AdNoir__Notification__Enabled']='false'
Add-Type -Path @((Join-Path $PSScriptRoot 'SessionLoadRunner.cs'), (Join-Path $PSScriptRoot 'SessionLoadResult.cs'))
try {
    $migrator=Start-App 'AdNoir.DbMigrator' 'src/Tools/AdNoir.DbMigrator' @{}
    if (-not $migrator.WaitForExit(60000) -or $migrator.ExitCode -ne 0) { throw 'Migrator failed' }
    $identity=Start-App 'AdNoir.IdentityAndOrg.Api' 'src/Services/IdentityAndOrg/AdNoir.IdentityAndOrg.Api' @{Kestrel__Endpoints__Http__Url='http://localhost:16380';Kestrel__Endpoints__Grpc__Url='http://localhost:16382'}
    Wait-Http 'http://localhost:16380/health/live'
    $null=Start-App 'AdNoir.Sample.Api' 'src/Services/Sample/AdNoir.Sample.Api' @{ASPNETCORE_URLS='http://localhost:16390'}
    Wait-Http 'http://localhost:16390/health/live'
    $token=Login 'adnoir' 'admin@adnoir.local'
    $url='http://localhost:16390/api/sample-items/00000000-0000-0000-0000-000000000001'
    $runner=[SessionLoadRunner]::new($token)
    $results=@()
    foreach($concurrency in @(1,5,10,20)) {
        $null=$runner.RunAsync($url,$concurrency,2,404).GetAwaiter().GetResult()
        $result=$runner.RunAsync($url,$concurrency,5,404).GetAwaiter().GetResult()
        $results+=$result
        Write-Host "Measured concurrency=$concurrency requests=$($result.Requests) errors=$($result.Errors) p95=$($result.P95Ms) ms"
        if($result.Errors -ne 0) { throw 'Unexpected healthy-load response; see local logs.' }
    }
    Stop-Process -Id $identity.Id
    $identity.WaitForExit()
    $outage=$runner.RunAsync($url,1,3,502).GetAwaiter().GetResult()
    if($outage.Errors -ne 0) { throw 'Identity outage did not produce controlled 502 envelopes.' }
    @{ measuredAtUtc=[DateTime]::UtcNow.ToString('O'); mode='Debug Development, loopback, no Redis, one tenant/user, warm caches'; durationPerStageSeconds=5; warmupSeconds=2; expectedHealthyStatus=404; reason='Known absent Sample item: full authentication, live-session RPC, permission and repository path'; stages=$results; outage=$outage } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $build 'session-load-results.json')
    Write-Host 'PASS controlled outage and all load stages'
} finally {
    if($runner) { $runner.Dispose() }
    foreach($child in $children) {
        if(-not $child.HasExited) { Stop-Process -Id $child.Id; $child.WaitForExit() }
    }
    if($dbName -notmatch '^adnoir_session_load_[a-f0-9]{32}$') { throw 'Unsafe cleanup target' }
    & $mongosh "mongodb://localhost:27017/$dbName`?replicaSet=rs0" --quiet --eval 'db.dropDatabase()'
    if($LASTEXITCODE -ne 0) { throw 'Scratch database cleanup failed' }
}
