# Runs inside the isolated role-security fixture; never logs response bodies or credentials.
function Invoke-BindingChecks($token) {
    $id = [Guid]::NewGuid().ToString()
    $cases = @(
        @{Name='malformed JSON'; Path='/api/auth/login'; Body='{"password":"binding-secret",'; Type='application/json'; Code=400},
        @{Name='missing body'; Path='/api/auth/login'; Body=''; Type='application/json'; Code=400},
        @{Name='wrong property type'; Path='/api/auth/login'; Body='{"tenantSlug":[],"email":"x","password":"binding-secret"}'; Type='application/json'; Code=400},
        @{Name='unsupported media'; Path='/api/auth/login'; Body='hello'; Type='text/plain'; Code=415},
        @{Name='text enum'; Path="/api/users/$id/roles"; Body='{"expectedVersion":1,"assignments":[{"roleId":"00000000-0000-0000-0000-000000000001","roleCode":"x","readScope":"tenant","writeScope":3}]}'; Type='application/json'; Code=400},
        @{Name='undefined numeric enum'; Path="/api/users/$id/roles"; Body='{"expectedVersion":1,"assignments":[{"roleId":"00000000-0000-0000-0000-000000000001","roleCode":"x","readScope":999,"writeScope":3}]}'; Type='application/json'; Code=400}
    )
    foreach ($origin in @('http://localhost:16180','http://localhost:16170')) {
        foreach ($case in $cases) {
            $method = if ($case.Path -like '*/roles') { 'PUT' } else { 'POST' }
            $response = Invoke-WebRequest -Uri "$origin$($case.Path)" -Method $method -Body $case.Body -ContentType $case.Type -Headers @{Authorization="Bearer $token"; 'X-Correlation-Id'='binding-fixture'} -SkipHttpErrorCheck -TimeoutSec 20
            if ([int]$response.StatusCode -ne $case.Code) { throw "$($case.Name): expected $($case.Code), got $($response.StatusCode)" }
            $body = $response.Content | ConvertFrom-Json
            $status = if ($case.Code -eq 400) { 'validationFailed' } else { 'unsupportedMediaType' }
            if ($body.isSuccess -ne $false -or $body.status -ne $status -or -not $body.messageCode -or -not $body.message -or -not $body.errors.Count -or $body.correlationId -ne 'binding-fixture' -or -not $body.traceId) { throw "$($case.Name): invalid response envelope" }
            if ($response.Content -match 'binding-secret|JsonException|BadHttpRequestException|System.Text.Json') { throw "$($case.Name): parser details disclosed" }
            Write-Host "PASS binding $($case.Name) via $origin"
        }
    }
    Write-Host 'PASS 12 request binding HTTP checks.'
}
