# Invoked by role-security.ps1 -AuditChecks against its disposable database only.
function Invoke-AuditChecks {
    $token=Login 'audit-a' 'admin@audit-a.test'
    $tenantA=(Check 'audit tenant A' GET '/api/v1/tenants/by-slug/audit-a' $null $token 200).data
    $tenantB=(Check 'audit tenant B' GET '/api/v1/tenants/by-slug/audit-b' $null $token 200).data
    $actor=((Check 'audit actor' GET '/api/v1/users/' $null $token 200).data | Where-Object email -eq 'admin@audit-a.test')
    $ids=@{a=$tenantA.id;b=$tenantB.id;actor=$actor.id;role=$own.id;foreign=$foreign.id}
    $auditChecks=0
    function Mongo-Check($label,$body) {
        if ($dbName -notmatch '^adnoir_role_security_[a-f0-9]{32}$') { throw 'Unexpected audit database' }
        $json=$ids | ConvertTo-Json -Compress
        $source="const ids=$json; function require(value) { if (!value) throw new Error('Audit invariant failed'); }"+[Environment]::NewLine+$body
        $file=Join-Path $run.FullName 'audit-check.js'
        Set-Content -LiteralPath $file -Value $source -Encoding utf8
        $uri='mongodb://localhost:27017/'+$dbName+'?replicaSet=rs0'
        $output=& $mongosh $uri --quiet --file $file 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Audit database check failed: $label" }
        # Never print database records or password material.
        Set-Variable -Name auditChecks -Value ($auditChecks+1) -Scope 1
        Write-Host "PASS audit: $label"
    }
    Mongo-Check 'role history and rejected foreign writes' @'
require(db.auditLogs.countDocuments({entityId:UUID(ids.role)})===3);
require(db.auditLogs.countDocuments({entityId:UUID(ids.foreign),action:"Created",tenantId:UUID(ids.b)})===1);
require(db.auditLogs.countDocuments({entityId:UUID(ids.foreign),tenantId:UUID(ids.a)})===0);
const update=db.auditLogs.findOne({entityId:UUID(ids.role),action:"Updated"});
require(update.changes.some(c=>c.field==="Name" && c.before==="Reader" && c.after==="Reader Updated"));
'@
    $created=(Check 'audited tenant create' POST '/api/v1/tenants/' @{name='Audit Target';slug='audit-target';hierarchyEnabled=$true;timeZoneId='UTC'} $token 201).data
    $ids.target=$created.id
    $null=Check 'audited tenant update' PUT "/api/v1/tenants/$($created.id)" @{name='Audit Updated';timeZoneId='UTC';status=1;expectedVersion=$created.version} $token 200
    Mongo-Check 'target tenant and actor tenant recorded separately' @'
const logs=db.auditLogs.find({entityId:UUID(ids.target)}).toArray();
require(logs.length===2 && db.auditLogs.countDocuments({entityId:UUID(ids.target),tenantId:UUID(ids.target),actorTenantId:UUID(ids.a),userId:UUID(ids.actor)})===2);
'@
    $person=(Check 'audited user create' POST '/api/v1/users/' @{email='audit-user@audit-a.test';displayName='Audit User';password=$password;orgUnitId=$actor.orgUnitId;mustChangePassword=$false} $token 201).data
    $ids.user=$person.id
    $null=Grant $person $tenantAdmin $token 200
    $person=(Check 'audit role reapplication version' GET "/api/v1/users/$($person.id)" $null $token 200).data
    $null=Grant $person $tenantAdmin $token 200
    $person=(Check 'audit user version' GET "/api/v1/users/$($person.id)" $null $token 200).data
    $null=Check 'audited user deactivate' POST "/api/v1/users/$($person.id)/deactivate?expectedVersion=$($person.version)" $null $token 200
    $person=(Check 'audit disabled user version' GET "/api/v1/users/$($person.id)" $null $token 200).data
    $null=Check 'audited user activate' POST "/api/v1/users/$($person.id)/activate?expectedVersion=$($person.version)" $null $token 200
    $userToken=Login 'audit-a' 'audit-user@audit-a.test'
    $newPassword='Bb2!'+[Guid]::NewGuid().ToString('N')
    $null=Check 'audited password change' POST '/api/v1/auth/change-password' @{currentPassword=$password;newPassword=$newPassword} $userToken 200
    Mongo-Check 'user history and masked password' @'
const logs=db.auditLogs.find({entityId:UUID(ids.user)}).toArray();
require(logs.length===6);
const password=logs.flatMap(l=>l.changes).find(c=>c.field==="Password");
require(password.before==="***" && password.after==="***");
const stamps=logs.flatMap(l=>l.changes).filter(c=>c.field==="SecurityStamp");
require(stamps.length===4 && stamps.every(c=>c.before==="***" && c.after==="***"));
require(logs.some(l=>l.changes.some(c=>c.field==="RoleAssignments")));
require(logs.some(l=>l.changes.some(c=>c.field==="IsActive" && c.before==="True" && c.after==="False")));
require(db.auditLogs.countDocuments({entityId:UUID(ids.user),tenantId:UUID(ids.a)})===6);
require(logs.every(l=>l.correlationId && l.occurredAtUtc instanceof Date));
require(logs.every(l=>l.changes.every(c=>!["PasswordHash","Email","DisplayName"].includes(c.field))));
'@
    $rootUnit=(Check 'audited org create' POST '/api/v1/org-units/' @{name='Audit Root';slug='audit-root'} $token 201).data
    $ids.org=$rootUnit.id
    $child=(Check 'audited child create' POST '/api/v1/org-units/' @{name='Audit Child';slug='audit-child';parentId=$rootUnit.id} $token 201).data
    $ids.child=$child.id
    $rootUnit=(Check 'audited org rename' PUT "/api/v1/org-units/$($rootUnit.id)" @{name='Renamed';slug='audit-renamed';code=$null;expectedVersion=$rootUnit.version} $token 200).data
    $rootUnit=(Check 'audited org move' POST "/api/v1/org-units/$($rootUnit.id)/move" @{newParentId=$actor.orgUnitId;expectedVersion=$rootUnit.version} $token 200).data
    $null=Check 'stale org edit has no audit' PUT "/api/v1/org-units/$($rootUnit.id)" @{name='Stale';slug=$null;code=$null;expectedVersion=1} $token 409
    $rootUnit=(Check 'audited name-only rename' PUT "/api/v1/org-units/$($rootUnit.id)" @{name='Final Name';slug=$null;code=$null;expectedVersion=$rootUnit.version} $token 200).data
    $child=(Check 'audit moved child' GET "/api/v1/org-units/$($child.id)" $null $token 200).data
    $null=Check 'audited org delete' DELETE "/api/v1/org-units/$($child.id)" $null $token 200
    Mongo-Check 'rename and move include descendant audit' @'
const logs=db.auditLogs.find({entityId:UUID(ids.child)}).toArray();
require(logs.length===4 && logs.filter(l=>l.action==="Updated").length===2 && logs.some(l=>l.action==="Deleted"));
require(logs.filter(l=>l.action==="Updated").every(l=>l.changes.some(c=>c.field==="HierarchyPath" && c.before!==c.after)));
require(db.auditLogs.countDocuments({entityId:UUID(ids.org)})===4);
'@
    Mongo-Check 'install audit failure fixture' @'
db.auditCheckState.insertOne({_id:"before",count:db.auditLogs.countDocuments({}),version:db.tenants.findOne({_id:UUID(ids.a)}).permissionVersion});
require(db.runCommand({collMod:"auditLogs",validator:{entityType:{$ne:"Role"}},validationLevel:"strict",validationAction:"error"}).ok===1);
'@
    try {
        $null=Check 'audit failure rejects role creation' POST '/api/v1/roles/' @{code='AuditRollback';name='Rollback';permissions=@('user:read')} $token 500
        Mongo-Check 'audit failure rolls back role and permission version' @'
const before=db.auditCheckState.findOne({_id:"before"});
require(db.roles.countDocuments({code:"AuditRollback"})===0);
require(db.tenants.findOne({_id:UUID(ids.a)}).permissionVersion.toString()===before.version.toString());
require(db.auditLogs.countDocuments({})===before.count);
'@
    } finally {
        Mongo-Check 'remove audit failure fixture' 'require(db.runCommand({collMod:"auditLogs",validator:{},validationLevel:"strict"}).ok===1);'
    }
    $headers=@{Authorization="Bearer $token";'X-Correlation-Id'='audit-correlation-fixture'}
    $reply=Invoke-WebRequest -Uri 'http://localhost:16170/api/v1/roles/' -Method Post -Headers $headers -ContentType 'application/json' -Body '{"code":"AuditRecovery","name":"Recovery","permissions":["user:read"]}' -SkipHttpErrorCheck
    if ($reply.StatusCode -ne 201) { throw 'Audit recovery failed' }
    $ids.recovery=($reply.Content | ConvertFrom-Json).data.id
    Mongo-Check 'audit recovers and shares response correlation' @'
const entry=db.auditLogs.findOne({entityId:UUID(ids.recovery)});
require(entry.correlationId==="audit-correlation-fixture" && db.auditLogs.countDocuments({entityId:UUID(ids.recovery),userId:UUID(ids.actor)})===1);
'@
    Write-Host "PASS $auditChecks audit database checks; audit recovery HTTP 201; no external data used."
}

