# Requires PowerShell 7.4+, a local MongoDB replica set and the solution built into artifacts/checks.
# Uses an isolated database and loopback ports; never uses the application's configured database.
param([string]$BuildRoot = 'artifacts/checks', [switch]$SessionChecks)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$build = Join-Path $root $BuildRoot
$dbName = 'adnoir_grpc_security_' + [Guid]::NewGuid().ToString('N')
$run = New-Item -ItemType Directory -Path (Join-Path $build $dbName)
$password = 'Aa1!' + [Guid]::NewGuid().ToString('N')
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$mongosh = (Get-Command mongosh -ErrorAction SilentlyContinue).Source
if (-not $mongosh) { $mongosh = Join-Path $env:LOCALAPPDATA 'Programs/mongosh/mongosh.exe' }
if (-not (Test-Path $mongosh)) { throw 'mongosh is required for isolated database cleanup.' }
foreach ($port in @(16170,16180,16182,16190,16200)) {
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
$serviceKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$notificationKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$deniedKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$scopedKey=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$allowedTenant=[Guid]::NewGuid().ToString()
$common['AdNoir__GrpcSecurity__Callers__0__Credential__ServiceId']='sample'
$common['AdNoir__GrpcSecurity__Callers__0__Credential__Key']=$serviceKey
$common['AdNoir__GrpcSecurity__Callers__0__CanReadPermissionMatrix']='true'
$common['AdNoir__GrpcSecurity__Callers__0__CanValidateUserSession']='true'
$common['AdNoir__GrpcSecurity__Callers__0__AllTenants']='true'
$common['AdNoir__GrpcSecurity__Callers__1__Credential__ServiceId']='notification'
$common['AdNoir__GrpcSecurity__Callers__1__Credential__Key']=$notificationKey
$common['AdNoir__GrpcSecurity__Callers__1__CanReadPermissionMatrix']='true'
$common['AdNoir__GrpcSecurity__Callers__1__CanValidateUserSession']='true'
$common['AdNoir__GrpcSecurity__Callers__1__AllTenants']='true'
$common['AdNoir__GrpcSecurity__Callers__2__Credential__ServiceId']='denied'
$common['AdNoir__GrpcSecurity__Callers__2__Credential__Key']=$deniedKey
$common['AdNoir__GrpcSecurity__Callers__2__AllTenants']='true'
$common['AdNoir__GrpcSecurity__Callers__3__Credential__ServiceId']='scoped'
$common['AdNoir__GrpcSecurity__Callers__3__Credential__Key']=$scopedKey
$common['AdNoir__GrpcSecurity__Callers__3__CanReadPermissionMatrix']='true'
$common['AdNoir__GrpcSecurity__Callers__3__TenantIds__0']=$allowedTenant
$common['AdNoir__Identity__ServiceCredentials__sample__ServiceId']='sample'
$common['AdNoir__Identity__ServiceCredentials__sample__Key']=$serviceKey
$common['AdNoir__Identity__ServiceCredentials__notification__ServiceId']='notification'
$common['AdNoir__Identity__ServiceCredentials__notification__Key']=$notificationKey
$common['AdNoir__RegistrationNotifications__PublishWelcomeEvent']='false'
$common['AdNoir__Notification__Enabled']='false'
$http=[Net.Http.HttpClient]::new()
$http.Timeout=[TimeSpan]::FromSeconds(10)
function Grpc-Check($label,$tenant,$id,$key,$expected,$duplicate=$false) {
    $request=[Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post,'http://localhost:16182/adnoir.identity.v1.UserService/GetPermissionMatrix')
    $request.Version=[Version]'2.0'
    $request.VersionPolicy=[Net.Http.HttpVersionPolicy]::RequestVersionExact
    $request.Headers.Add('te','trailers')
    if ($id) { $request.Headers.Add('x-adnoir-service-id',$id) }
    if ($key) { $request.Headers.Add('x-adnoir-service-key',$key) }
    if ($duplicate) { $request.Headers.Add('x-adnoir-service-id','sample') }
    $bytes=[Text.Encoding]::UTF8.GetBytes($tenant)
    [byte[]]$message=@(0,0,0,0,($bytes.Length+2),10,$bytes.Length)+$bytes
    $request.Content=[Net.Http.ByteArrayContent]::new($message)
    $request.Content.Headers.ContentType=[Net.Http.Headers.MediaTypeHeaderValue]::new('application/grpc')
    try {
        $response=$http.SendAsync($request).GetAwaiter().GetResult()
        $null=$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $status=if ($response.TrailingHeaders.Contains('grpc-status')) { @($response.TrailingHeaders.GetValues('grpc-status'))[0] } elseif ($response.Headers.Contains('grpc-status')) { @($response.Headers.GetValues('grpc-status'))[0] } else { 'missing' }
        if ($status -ne "$expected") { throw "$label expected gRPC $expected got $status" }
        $script:checks++
        Write-Host "PASS $label (gRPC $status)"
    } finally { if ($response) { $response.Dispose() }; $request.Dispose() }
}
try {
    $migrator=Start-App 'AdNoir.DbMigrator' 'src/Tools/AdNoir.DbMigrator' @{}
    $migrator.WaitForExit()
    if ($migrator.ExitCode -ne 0) { throw 'Migrator failed' }
    $null=Start-App 'AdNoir.IdentityAndOrg.Api' 'src/Services/IdentityAndOrg/AdNoir.IdentityAndOrg.Api' @{Kestrel__Endpoints__Http__Url='http://localhost:16180';Kestrel__Endpoints__Grpc__Url='http://localhost:16182'}
    Wait-Http 'http://localhost:16180/health/live'
    $null=Start-App 'AdNoir.Gateway' 'src/Gateway/AdNoir.Gateway' @{ASPNETCORE_URLS='http://localhost:16170';ReverseProxy__Clusters__identity__Destinations__primary__Address='http://localhost:16180/';ReverseProxy__Clusters__sample__Destinations__primary__Address='http://localhost:16190/';ReverseProxy__Clusters__notification__Destinations__primary__Address='http://localhost:16200/'}
    Wait-Http 'http://localhost:16170/health/live'
    $token=Login 'adnoir' 'admin@adnoir.local'
    $tenants=(Check 'get seeded tenant' GET '/api/tenants/' $null $token 200).data
    $tenant=$tenants[0].id
    if (-not $tenant) { throw 'Tenant ID missing' }
    Grpc-Check 'missing credential denied' $tenant $null $null 16
    Grpc-Check 'unknown service denied' $tenant 'unknown' $serviceKey 16
    Grpc-Check 'wrong key denied' $tenant 'sample' $deniedKey 16
    Grpc-Check 'authenticated unauthorized service denied' $tenant 'denied' $deniedKey 7
    Grpc-Check 'tenant outside allowlist denied' $tenant 'scoped' $scopedKey 7
    Grpc-Check 'allowed but nonexistent tenant' $allowedTenant 'scoped' $scopedKey 5
    Grpc-Check 'sample authorized' $tenant 'sample' $serviceKey 0
    Grpc-Check 'notification authorized' $tenant 'notification' $notificationKey 0
    Grpc-Check 'duplicate identity denied' $tenant 'sample' $serviceKey 16 $true
    # Reload the allowlist through a server restart, then verify a real tenant succeeds.
    Stop-Process -Id $children[1].Id
    $children[1].WaitForExit()
    $common['AdNoir__GrpcSecurity__Callers__3__TenantIds__0']=$tenant
    $null=Start-App 'AdNoir.IdentityAndOrg.Api' 'src/Services/IdentityAndOrg/AdNoir.IdentityAndOrg.Api' @{Kestrel__Endpoints__Http__Url='http://localhost:16180';Kestrel__Endpoints__Grpc__Url='http://localhost:16182'}
    Wait-Http 'http://localhost:16180/health/live'
    Grpc-Check 'explicit tenant allowlist succeeds' $tenant 'scoped' $scopedKey 0
    Grpc-Check 'other tenant still denied' $allowedTenant 'scoped' $scopedKey 7
    # Development signing keys are ephemeral, so refresh the login after restarting Identity.
    Stop-Process -Id $children[2].Id
    $children[2].WaitForExit()
    $null=Start-App 'AdNoir.Gateway' 'src/Gateway/AdNoir.Gateway' @{ASPNETCORE_URLS='http://localhost:16170';ReverseProxy__Clusters__identity__Destinations__primary__Address='http://localhost:16180/';ReverseProxy__Clusters__sample__Destinations__primary__Address='http://localhost:16190/';ReverseProxy__Clusters__notification__Destinations__primary__Address='http://localhost:16200/'}
    Wait-Http 'http://localhost:16170/health/live'
    $token=Login 'adnoir' 'admin@adnoir.local'
    $missing=Start-App 'AdNoir.Sample.Api' 'src/Services/Sample/AdNoir.Sample.Api' @{ASPNETCORE_URLS='http://localhost:16190';AdNoir__Identity__ServiceCredentials__sample__Key=''}
    if (-not $missing.WaitForExit(10000) -or $missing.ExitCode -eq 0) { throw 'Missing client credential did not fail startup.' }
    $script:checks++
    Write-Host 'PASS missing client credential fails startup'
    $null=Start-App 'AdNoir.Sample.Api' 'src/Services/Sample/AdNoir.Sample.Api' @{ASPNETCORE_URLS='http://localhost:16190'}
    Wait-Http 'http://localhost:16190/health/live'
    $null=Start-App 'AdNoir.Notification.Api' 'src/Services/Notification/AdNoir.Notification.Api' @{ASPNETCORE_URLS='http://localhost:16200'}
    Wait-Http 'http://localhost:16200/health/live'
    $null=Check 'Sample cold permission cache uses service credential' GET '/api/sample-items/00000000-0000-0000-0000-000000000001' $null $token 404
    $null=Check 'Notification cold permission cache uses service credential' GET '/api/notifications/emails/' $null $token 200
    if ($SessionChecks) {
        $null=Check 'anonymous password change denied' POST '/api/auth/change-password' @{currentPassword=$password;newPassword='Different123!'} $null 401
        $users=(Check 'get administrator unit' GET '/api/users/' $null $token 200).data
        $owner=$users | Where-Object email -eq 'admin@adnoir.local'
        $roles=(Check 'get tenant administrator role' GET '/api/roles/' $null $token 200).data
        $role=$roles | Where-Object code -eq 'TenantAdmin'
        $user=(Check 'create mandatory password user' POST '/api/users/' @{email='forced@example.test';displayName='Forced';password=$password;orgUnitId=$owner.orgUnitId;mustChangePassword=$true} $token 201).data
        $null=Grant $user $role $token 200
        $login=Check 'forced user login' POST '/api/auth/login' @{tenantSlug='adnoir';email='forced@example.test';password=$password} $null 200
        $forced=$login.data.accessToken
        foreach($path in @('/api/users/','/api/notifications/emails/','/api/sample-items/00000000-0000-0000-0000-000000000001')) {
            $denial=Check 'forced password blocks business access' GET $path $null $forced 403
            if ($denial.messageCode -ne 'identity.password.change_required') { throw 'Password-change denial code missing.' }
        }
        $renewed=Check 'refresh preserves password requirement' POST '/api/auth/refresh' @{refreshToken=$login.data.refreshToken} $null 200
        $null=Check 'refreshed forced session still blocked' GET '/api/users/' $null $renewed.data.accessToken 403
        $newPassword='Bb2!' + [Guid]::NewGuid().ToString('N')
        $null=Check 'required password change allowed' POST '/api/auth/change-password' @{currentPassword=$password;newPassword=$newPassword} $forced 200
        foreach($path in @('/api/users/','/api/notifications/emails/','/api/sample-items/00000000-0000-0000-0000-000000000001')) {
            $null=Check 'old token rejected after password change' GET $path $null $forced 401
        }
        $null=Check 'old refresh rejected after password change' POST '/api/auth/refresh' @{refreshToken=$renewed.data.refreshToken} $null 401
        $login=Check 'new password login' POST '/api/auth/login' @{tenantSlug='adnoir';email='forced@example.test';password=$newPassword} $null 200
        $fresh=$login.data.accessToken
        $null=Check 'new session business access restored' GET '/api/users/' $null $fresh 200
        $user=(Check 'get version before role replacement' GET "/api/users/$($user.id)" $null $token 200).data
        $user=(Grant $user $role $token 200).data
        $null=Check 'role change invalidates old token' GET '/api/notifications/emails/' $null $fresh 401
        $login=Check 'role change allows fresh login' POST '/api/auth/login' @{tenantSlug='adnoir';email='forced@example.test';password=$newPassword} $null 200
        $fresh=$login.data.accessToken
        $user=(Check 'read current version before disabling' GET "/api/users/$($user.id)" $null $token 200).data
        $null=Check 'disable account' POST "/api/users/$($user.id)/deactivate?expectedVersion=$($user.version)" $null $token 200
        $null=Check 'disabled account token denied by Sample' GET '/api/sample-items/00000000-0000-0000-0000-000000000001' $null $fresh 401
        $null=Check 'disabled account refresh denied' POST '/api/auth/refresh' @{refreshToken=$login.data.refreshToken} $null 401
        # Stop only our restarted Identity process; cached permissions must not bypass live validation.
        # Identity is the first process added after the initial gateway.
        Stop-Process -Id $children[3].Id
        $children[3].WaitForExit()
        $null=Check 'Identity outage fails closed despite warm cache' GET '/api/notifications/emails/' $null $token 502
    }
    Write-Host "PASS $script:checks checks; no unit tests."
} finally {
    $http.Dispose()
    foreach ($child in $children) { if (-not $child.HasExited) { Stop-Process -Id $child.Id; $child.WaitForExit() } }
    if ($dbName -notmatch '^adnoir_grpc_security_[a-f0-9]{32}$') { throw 'Unsafe cleanup database name' }
    & $mongosh "mongodb://localhost:27017/$dbName`?replicaSet=rs0" --quiet --eval "if (db.getName() !== '$dbName') throw new Error('Unexpected database'); db.dropDatabase();"
    if ($LASTEXITCODE -ne 0) { throw "Cleanup failed for $dbName" }
}
