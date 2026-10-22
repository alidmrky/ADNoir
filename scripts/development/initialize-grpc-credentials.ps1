# Creates local-only credentials without printing or overwriting existing secrets.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$path = Join-Path $root '.env'
$existing = if (Test-Path $path) { [IO.File]::ReadAllText($path) } else { '' }
if ($existing -match '(?m)^\s*AdNoir__(GrpcSecurity__|Identity__ServiceCredentials__)') {
    throw 'gRPC settings already exist. Review them manually; credentials were not overwritten.'
}
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('')
$lines.Add('# Local gRPC service identities; never commit this file.')
$index = 0
foreach ($service in @('sample','notification')) {
    $key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $lines.Add("AdNoir__Identity__ServiceCredentials__$($service)__ServiceId=$service")
    $lines.Add("AdNoir__Identity__ServiceCredentials__$($service)__Key=$key")
    $lines.Add("AdNoir__GrpcSecurity__Callers__$($index)__Credential__ServiceId=$service")
    $lines.Add("AdNoir__GrpcSecurity__Callers__$($index)__Credential__Key=$key")
    $lines.Add("AdNoir__GrpcSecurity__Callers__$($index)__CanReadPermissionMatrix=true")
    $lines.Add("AdNoir__GrpcSecurity__Callers__$($index)__CanValidateUserSession=true")
    $lines.Add("AdNoir__GrpcSecurity__Callers__$($index)__AllTenants=true")
    $index++
}
[IO.File]::WriteAllText($path, $existing.TrimEnd() + [Environment]::NewLine + ($lines -join [Environment]::NewLine) + [Environment]::NewLine)
Write-Host 'Local service credentials added to .env. Restart Identity, Sample and Notification.'
