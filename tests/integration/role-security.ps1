# Requires PowerShell 7.4+, a local MongoDB replica set and the solution built into artifacts/checks.
# Uses an isolated database and loopback ports; never uses the application's configured database.
param([string]$BuildRoot = 'artifacts/checks', [switch]$AuditChecks, [switch]$BindingChecks)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$build = Join-Path $root $BuildRoot
$dbName = 'adnoir_role_security_' + [Guid]::NewGuid().ToString('N')
$run = New-Item -ItemType Directory -Path (Join-Path $build $dbName)
$password = 'Aa1!' + [Guid]::NewGuid().ToString('N')
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$mongosh = (Get-Command mongosh -ErrorAction SilentlyContinue).Source
if (-not $mongosh) { $mongosh = Join-Path $env:LOCALAPPDATA 'Programs/mongosh/mongosh.exe' }
if (-not (Test-Path $mongosh)) { throw 'mongosh is required for isolated database cleanup.' }
foreach ($port in @(16170,16180,16182)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port is already in use." }
}
$common = @{
    DOTNET_ENVIRONMENT='Development'; ASPNETCORE_ENVIRONMENT='Development'
    AdNoir__Mongo__ConnectionString='mongodb://localhost:27017/?replicaSet=rs0'
    AdNoir__Mongo__DatabaseName=$dbName
    AdNoir__RabbitMq__Host=''; AdNoir__Redis__ConnectionString=''
    AdNoir__Jwt__Issuer='http://localhost:16180'; AdNoir__Jwt__Audience='adnoir'
    AdNoir__Jwt__JwksUri='http://localhost:16180/.well-known/jwks.json'
    AdNoir__Jwt__PrivateKeyPem=''; AdNoir__Seed__SuperAdminPassword=$password
    AdNoir__Identity__GrpcAddress='http://localhost:16182'
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
    $parameters=@{Uri="http://localhost:16170$path"; Method=$method; Headers=$headers; SkipHttpErrorCheck=$true; TimeoutSec=20}
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
function Grant($user,$role,$token,$expected, $scope=3) {
    return Check 'role assignment' PUT "/api/users/$($user.id)/roles" @{
        expectedVersion=$user.version
        assignments=@(@{roleId=$role.id;roleCode=$role.code;readScope=$scope;writeScope=$scope})
    } $token $expected
}
try {
    foreach ($slug in @('audit-a','audit-b')) {
        $migrator=Start-App 'AdNoir.DbMigrator' 'src/Tools/AdNoir.DbMigrator' @{
            AdNoir__Seed__RootTenantSlug=$slug;AdNoir__Seed__RootTenantName=$slug
            AdNoir__Seed__SuperAdminEmail="admin@$slug.test"
        }
        $migrator.WaitForExit()
        if ($migrator.ExitCode -ne 0) { throw "Migrator failed for $slug" }
    }
    $null=Start-App 'AdNoir.IdentityAndOrg.Api' 'src/Services/IdentityAndOrg/AdNoir.IdentityAndOrg.Api' @{
        Kestrel__Endpoints__Http__Url='http://localhost:16180'
        Kestrel__Endpoints__Grpc__Url='http://localhost:16182'
    }
    Wait-Http 'http://localhost:16180/health/live'
    $null=Start-App 'AdNoir.Gateway' 'src/Gateway/AdNoir.Gateway' @{
        ASPNETCORE_URLS='http://localhost:16170'
        ReverseProxy__Clusters__identity__Destinations__primary__Address='http://localhost:16180/'
    }
    Wait-Http 'http://localhost:16170/health/live'
    $a=Login 'audit-a' 'admin@audit-a.test'
    $b=Login 'audit-b' 'admin@audit-b.test'
    if ($BindingChecks) {
        . (Join-Path $PSScriptRoot 'binding-checks.ps1')
        Invoke-BindingChecks $a
    }
    $foreign=(Check 'create tenant B role' POST '/api/roles/' @{code='Foreign';name='Foreign';permissions=@('user:read')} $b 201).data
    $b=Login 'audit-b' 'admin@audit-b.test'
    $null=Check 'foreign role read denied' GET "/api/roles/$($foreign.id)" $null $a 404
    $null=Check 'foreign role update denied' PUT "/api/roles/$($foreign.id)" @{name='Changed';permissions=@('user:read');expectedVersion=$foreign.version} $a 404
    $null=Check 'foreign role delete denied' DELETE "/api/roles/$($foreign.id)" $null $a 404
    $after=(Check 'foreign role unchanged' GET "/api/roles/$($foreign.id)" $null $b 200).data
    if ($after.name -ne $foreign.name -or $after.version -ne $foreign.version) { throw 'Foreign role mutated.' }
    $roles=(Check 'global system roles visible' GET '/api/roles/' $null $a 200).data
    $super=$roles | Where-Object code -eq 'SuperAdmin'
    $tenantAdmin=$roles | Where-Object code -eq 'TenantAdmin'
    if (-not $super -or -not $tenantAdmin) { throw 'Missing system roles.' }
    $admin=(Check 'register tenant administrator' POST '/api/auth/register' @{tenantSlug='audit-a';email='tenant@audit-a.test';password=$password;displayName='Tenant Admin'} $null 201).data
    $member=(Check 'register member' POST '/api/auth/register' @{tenantSlug='audit-a';email='member@audit-a.test';password=$password;displayName='Member'} $null 201).data
    $null=Grant $admin $tenantAdmin $a 200
    $ta=Login 'audit-a' 'tenant@audit-a.test'
    $null=Grant $member $super $ta 403
    $null=Check 'platform permission forbidden in custom roles' POST '/api/roles/' @{code='Escalated';name='Escalated';permissions=@('tenant:manage')} $ta 403
    $null=Check 'reserved role code forbidden' POST '/api/roles/' @{code='superadmin';name='Reserved';permissions=@('user:read')} $ta 403
    $own=(Check 'allowed tenant role creation' POST '/api/roles/' @{code='Reader';name='Reader';permissions=@('user:read')} $ta 201).data
    $ta=Login 'audit-a' 'tenant@audit-a.test'
    $null=Check 'role update cannot gain platform permission' PUT "/api/roles/$($own.id)" @{name='Escalated';permissions=@('tenant:manage');expectedVersion=$own.version} $ta 403
    $own=(Check 'allowed tenant role update' PUT "/api/roles/$($own.id)" @{name='Reader Updated';permissions=@('user:read');expectedVersion=$own.version} $ta 200).data
    $ta=Login 'audit-a' 'tenant@audit-a.test'
    $null=Check 'allowed tenant role delete' DELETE "/api/roles/$($own.id)" $null $ta 200
    $ta=Login 'audit-a' 'tenant@audit-a.test'
    $null=Check 'soft deleted role hidden' GET "/api/roles/$($own.id)" $null $ta 404
    $a=Login 'audit-a' 'admin@audit-a.test'
    $allUsers=(Check 'find seeded platform administrator' GET '/api/users/' $null $a 200).data
    # PagedResponse stores the user collection directly under data.
    $seeded=$allUsers | Where-Object email -eq 'admin@audit-a.test'
    if (-not $seeded) { throw 'Seeded administrator not found in user page.' }
    $null=Grant $seeded $tenantAdmin $ta 403
    $limited=(Check 'create limited delegation role' POST '/api/roles/' @{code='Delegate';name='Delegate';permissions=@('user:manage','user:read','role:read','role:manage')} $ta 201).data
    $ta=Login 'audit-a' 'tenant@audit-a.test'
    $admin=(Check 'read tenant administrator version' GET "/api/users/$($admin.id)" $null $ta 200).data
    $null=Grant $admin $limited $ta 200 1
    $null=Check 'stale actor cannot create role' POST '/api/roles/' @{code='Stale';name='Stale';permissions=@('user:read')} $ta 401
    $restricted=Login 'audit-a' 'tenant@audit-a.test'
    $null=Check 'unit scope cannot edit tenant-wide definitions' POST '/api/roles/' @{code='Wide';name='Wide';permissions=@('user:read')} $restricted 403
    $self=(Check 'same unit account remains accessible' GET "/api/users/$($admin.id)" $null $restricted 200).data
    $null=Grant $self $limited $restricted 403 3
    $null=Grant $self $tenantAdmin $restricted 403 1
    if ($AuditChecks) {
        . (Join-Path $PSScriptRoot 'audit-checks.ps1')
        Invoke-AuditChecks
    }
    Write-Host "PASS $script:checks HTTP checks; two tenant seeds; no unit tests."
} finally {
    foreach ($child in $children) {
        if (-not $child.HasExited) { Stop-Process -Id $child.Id; $child.WaitForExit() }
    }
    if ($dbName -notmatch '^adnoir_role_security_[a-f0-9]{32}$') { throw 'Unsafe cleanup database name' }
    $cleanup="if (db.getName() !== '$dbName') throw new Error('Unexpected database'); db.dropDatabase();"
    & $mongosh "mongodb://localhost:27017/$dbName`?replicaSet=rs0" --quiet --eval $cleanup
    if ($LASTEXITCODE -ne 0) { throw "Cleanup failed for $dbName" }
}
