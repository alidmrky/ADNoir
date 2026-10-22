(async () => {
// Run from the repository root using mongosh --nodb --file. Synthetic data only.
const fs = require('fs');
const crypto = require('crypto');
const databaseName = 'adnoir_notification_perf_' + crypto.randomBytes(16).toString('hex');
const connection = await new Mongo('mongodb://localhost:27017/?replicaSet=rs0');
const database = connection.getDB(databaseName);
const collection = database.getCollection('emailDeliveries');
const tenant = UUID('00000000-0000-4000-8000-000000000001');
const otherTenant = UUID('00000000-0000-4000-8000-000000000002');
const now = new Date('2026-10-21T00:00:00Z');
const source = fs.readFileSync('src/Services/Notification/AdNoir.Notification.Infrastructure/Persistence/MongoEmailRepository.cs', 'utf8');
const groupMatch = source.match(/BsonDocument stage = BsonDocument.Parse\("""([\s\S]*?)"""\)/);
if (!groupMatch) throw new Error('Statistics pipeline source changed; update the measurement.');
const group = JSON.parse(groupMatch[1]);
const sort = {createdAtUtc:-1, _id:-1};
const filters = {all: {tenantId:tenant}, status:{tenantId:tenant,status:'Accepted'}};
const pipeline = [{$match:{tenantId:tenant,createdAtUtc:{$gte:new Date(now-30*86400000),$lt:now}}},group];
async function summarize(explain) {
    explain = await explain;
    const cursor = (Array.isArray(explain.stages) ? explain.stages.find(stage=>stage.$cursor)?.$cursor : null) ?? explain;
    if (!cursor.executionStats) throw new Error('Missing execution stats: '+JSON.stringify(explain));
    const stats = cursor.executionStats;
    const plan = cursor.queryPlanner?.winningPlan;
    return {returned:stats?.nReturned, keys:stats?.totalKeysExamined, docs:stats?.totalDocsExamined,
        serverMs:stats?.executionTimeMillis, blockingSort:JSON.stringify(plan).includes('"stage":"SORT"'),plan};
}
async function timing(action) {
    for(let i=0;i<3;i++) await action();
    const samples=[];
    for(let i=0;i<30;i++) {const start=process.hrtime.bigint();await action();samples.push(Number(process.hrtime.bigint()-start)/1e6);}
    samples.sort((a,b)=>a-b);
    return {samples:30,p50Ms:samples[14],p95Ms:samples[28],maxMs:samples[29]};
}
async function measure(label) {
    const result={label,indexBytes:(await database.runCommand({collStats:collection.getName()})).totalIndexSize,queries:{}};
    for(const [name,filter] of Object.entries(filters)) {
        const read=()=>collection.find(filter).sort(sort).limit(100).toArray();
        result.queries[name]={listPlan:await summarize(database.runCommand({explain:{find:collection.getName(),filter,sort,limit:100},verbosity:'executionStats'})),
            countPlan:await summarize(database.runCommand({explain:{aggregate:collection.getName(),pipeline:[{$match:filter},{$count:'count'}],cursor:{}},verbosity:'executionStats'})),
            list:await timing(read),count:await timing(()=>collection.countDocuments(filter)),
            countAndList:await timing(async()=>{await collection.countDocuments(filter);await read();})};
    }
    result.statistics={plan:await summarize(database.runCommand({explain:{aggregate:collection.getName(),pipeline,cursor:{}},verbosity:'executionStats'})),
        latency:await timing(()=>collection.aggregate(pipeline,{maxTimeMS:10000}).toArray())};
    return result;
}
let ownsDatabase = false;
try {
    if((await database.getCollectionNames()).length) throw new Error('Scratch database already exists.');
    ownsDatabase = true;
    // Mirrors the deployed pre-optimization index set; candidate additions are measured separately.
    await collection.createIndex({tenantId:1,sourceEventId:1,kind:1},{name:'ux_email_event_kind',unique:true});
    await collection.createIndex({tenantId:1,createdAtUtc:-1},{name:'ix_email_tenant_created'});
    await collection.createIndex({tenantId:1,status:1,nextAttemptAtUtc:1},{name:'ix_email_due'});
    await collection.createIndex({tenantId:1,status:1,leaseUntilUtc:1},{name:'ix_email_lease'});
    await collection.createIndex({tenantId:1,isRedacted:1,redactAtUtc:1},{name:'ix_email_retention'});
    for(let offset=0;offset<100000;offset+=1000) {
        const batch=[];
        for(let i=offset;i<offset+1000;i++) {
            const createdAtUtc=new Date(now-Math.floor(i/100)*5184000);
            batch.push({_id:UUID(),tenantId:i%5===0?otherTenant:tenant,sourceEventId:UUID(),kind:'welcome',
                version:Long(1),createdAtUtc,status:i%7===0?'Accepted':'Pending',isRedacted:false,
                recipient:'fixture@example.test',subject:'Synthetic',body:'x'.repeat(1024),
                nextAttemptAtUtc:createdAtUtc,redactAtUtc:new Date(+createdAtUtc+90*86400000),
                attempts:i%7===0?[{startedAtUtc:new Date(+createdAtUtc+1000),durationMilliseconds:20}]:[]});
        }
        await collection.insertMany(batch);
    }
    const beforeIds={};
    for (const [name,filter] of Object.entries(filters)) {
        beforeIds[name]=(await collection.find(filter).sort(sort).limit(100).toArray()).map(item=>item._id.toString());
    }
    const beforeStats=EJSON.stringify((await collection.aggregate(pipeline).toArray()).sort((a,b)=>a._id.localeCompare(b._id)));
    const before=await measure('baseline');
    await collection.createIndex({tenantId:1,createdAtUtc:-1,_id:-1},{name:'ix_email_tenant_created_id'});
    await collection.createIndex({tenantId:1,status:1,createdAtUtc:-1,_id:-1},{name:'ix_email_tenant_status_created_id'});
    const after=await measure('candidate');
    for(const [name,filter] of Object.entries(filters)) {
        const ids=(await collection.find(filter).sort(sort).limit(100).toArray()).map(item=>item._id.toString());
        if(JSON.stringify(ids)!==JSON.stringify(beforeIds[name])) throw new Error('List ordering changed');
        if(after.queries[name].listPlan.blockingSort || after.queries[name].listPlan.docs>100) throw new Error('Candidate did not bound the list scan');
    }
    if(beforeStats!==EJSON.stringify((await collection.aggregate(pipeline).toArray()).sort((a,b)=>a._id.localeCompare(b._id)))) throw new Error('Statistics changed');
    print(JSON.stringify({mongoVersion:await database.version(),documents:100000,tenantDocuments:80000,windowDays:60,
        payloadBytes:1024,timing:'mongosh client wall time; sequential warm runs, no HTTP or concurrent load',before,after},null,2));
} finally {
    if(!/^adnoir_notification_perf_[a-f0-9]{32}$/.test(databaseName)) throw new Error('Unsafe cleanup name');
    if (ownsDatabase) await database.dropDatabase();
}

})().catch(error => { console.error(error); quit(1); });
