# Requires PowerShell 7.4+, a local MongoDB replica set and the solution built into artifacts/checks.
# Uses an isolated database and loopback ports; never uses the application's configured database.
param([string]$BuildRoot = 'artifacts/checks')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$build = Join-Path $root $BuildRoot
$dbName = 'adnoir_broker_flow_' + [Guid]::NewGuid().ToString('N')
$run = New-Item -ItemType Directory -Path (Join-Path $build $dbName)
$password = 'Aa1!' + [Guid]::NewGuid().ToString('N')
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$mongosh = (Get-Command mongosh -ErrorAction SilentlyContinue).Source
if (-not $mongosh) { $mongosh = Join-Path $env:LOCALAPPDATA 'Programs/mongosh/mongosh.exe' }
if (-not (Test-Path $mongosh)) { throw 'mongosh is required for isolated database cleanup.' }
foreach ($port in @(16480,16482,16400)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port is already in use." }
}
$common = @{
    DOTNET_ENVIRONMENT='Development'; ASPNETCORE_ENVIRONMENT='Development'
    AdNoir__Mongo__ConnectionString='mongodb://localhost:27017/?replicaSet=rs0'
    AdNoir__Mongo__DatabaseName=$dbName
    AdNoir__RabbitMq__Host=''; AdNoir__Redis__ConnectionString=''
    AdNoir__Jwt__Issuer='http://localhost:16480'; AdNoir__Jwt__Audience='adnoir'
    AdNoir__Jwt__JwksUri='http://localhost:16480/.well-known/jwks.json'
    AdNoir__Jwt__PrivateKeyPem=''; AdNoir__Seed__SuperAdminPassword=$password
    AdNoir__Identity__GrpcAddress='http://localhost:16482'
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
    $parameters=@{Uri="http://localhost:16480$path"; Method=$method; Headers=$headers; SkipHttpErrorCheck=$true; TimeoutSec=20}
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


$common['AdNoir__RabbitMq__Host']='localhost'
$common['AdNoir__RabbitMq__Port']='16672'
$common['AdNoir__RabbitMq__Username']='guest'
$common['AdNoir__RabbitMq__Password']='guest'
$common['AdNoir__RegistrationNotifications__PublishWelcomeEvent']='true'
$key=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$common['AdNoir__GrpcSecurity__Callers__0__Credential__ServiceId']='notification'
$common['AdNoir__GrpcSecurity__Callers__0__Credential__Key']=$key
$common['AdNoir__GrpcSecurity__Callers__0__CanReadPermissionMatrix']='true'
$common['AdNoir__GrpcSecurity__Callers__0__CanValidateUserSession']='true'
$common['AdNoir__GrpcSecurity__Callers__0__AllTenants']='true'
$common['AdNoir__Identity__ServiceCredentials__notification__ServiceId']='notification'
$common['AdNoir__Identity__ServiceCredentials__notification__Key']=$key
function Mongo-Assert($javascript,$label) {
 $output=& $mongosh "mongodb://localhost:27017/$dbName`?replicaSet=rs0" --quiet --eval $javascript 2>&1
 if($LASTEXITCODE -ne 0){throw $label}
 Write-Host "PASS $label"
}
function Wait-Deliveries($count) {
 for($i=0;$i -lt 60;$i++) {
  $output=& $mongosh "mongodb://localhost:27017/$dbName`?replicaSet=rs0" --quiet --eval "db.emailDeliveries.countDocuments({})" 2>&1
  if($LASTEXITCODE -eq 0 -and "$output".Trim() -eq "$count"){Write-Host "PASS $count notification records delivered";return}
  Start-Sleep -Milliseconds 500
 }
 throw 'Notification delivery timed out'
}
function Start-Identity {
 $process=Start-App 'AdNoir.IdentityAndOrg.Api' 'src/Services/IdentityAndOrg/AdNoir.IdentityAndOrg.Api' @{Kestrel__Endpoints__Http__Url='http://localhost:16480';Kestrel__Endpoints__Grpc__Url='http://localhost:16482'}
 Wait-Http 'http://localhost:16480/health/live'
 return $process
}
try {
 $migrator=Start-App 'AdNoir.DbMigrator' 'src/Tools/AdNoir.DbMigrator' @{}
 if(-not $migrator.WaitForExit(60000) -or $migrator.ExitCode -ne 0){throw 'Migrator failed'}
 $identity=Start-Identity
 $null=Start-App 'AdNoir.Notification.Api' 'src/Services/Notification/AdNoir.Notification.Api' @{
  ASPNETCORE_URLS='http://localhost:16400';AdNoir__Notification__Enabled='true';AdNoir__Notification__Smtp__Host='127.0.0.1';AdNoir__Notification__Smtp__Port='25252';AdNoir__Notification__Smtp__From='fixture@example.test';AdNoir__Notification__Smtp__EnableSsl='false'
 }
 Wait-Http 'http://localhost:16400/health/live'
 $null=Check 'register with broker online' POST '/api/auth/register' @{tenantSlug='adnoir';email='online@example.test';displayName='Fixture';password=$password} $null 201
 Wait-Deliveries 1
 & "$PSScriptRoot/isolated-broker.ps1" -Stop
 if(Get-NetTCPConnection -LocalPort 16672 -State Listen -ErrorAction SilentlyContinue){throw 'Test broker still listening'}
 $null=Check 'register commits while broker unavailable' POST '/api/auth/register' @{tenantSlug='adnoir';email='offline@example.test';displayName='Fixture';password=$password} $null 201
 Mongo-Assert 'if(db.users.countDocuments({email:"offline@example.test"})!==1 || db.emailDeliveries.countDocuments({})!==1) throw new Error("Mismatch")' 'offline business commit without notification delivery'
 Stop-Process -Id $identity.Id
 $identity.WaitForExit()
 & "$PSScriptRoot/isolated-broker.ps1"
 for($i=0;$i -lt 60;$i++){if(Get-NetTCPConnection -LocalPort 16672 -State Listen -ErrorAction SilentlyContinue){break};Start-Sleep -Milliseconds 500}
 $identity=Start-Identity
 Wait-Deliveries 2
 Mongo-Assert 'if(db.emailDeliveries.countDocuments({status:"Skipped"})!==2 || db.emailDeliveries.countDocuments({"attempts.0":{$exists:true}})!==0) throw new Error("Unexpected SMTP")' 'recovered notifications are unique and no SMTP attempted'
 Write-Host 'PASS real Identity HTTP -> outbox -> RabbitMQ -> Notification, broker outage and producer restart'
} finally {
 foreach($child in $children){if(-not $child.HasExited){Stop-Process -Id $child.Id;$child.WaitForExit()}}
 if($dbName -notmatch '^adnoir_broker_flow_[a-f0-9]{32}$'){throw 'Unsafe database cleanup target'}
 & $mongosh "mongodb://localhost:27017/$dbName`?replicaSet=rs0" --quiet --eval 'db.dropDatabase()'
 if($LASTEXITCODE -ne 0){throw 'Database cleanup failed'}
}
