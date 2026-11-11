"""Isolated 100k-member, 50k-recipient, conversation/history and restart benchmark."""
import os,pathlib,time,uuid,json,statistics,subprocess,concurrent.futures,urllib.request
import support
DB='clubs_governance_scale';support.BASE='http://127.0.0.1:5087'
from support import Client
sql=lambda q:support.sql(q,DB)
def good(c,p,m='GET',d=None):
 code,b=c.req('/communications'+p,m,d);assert code==200,(p,code,str(b)[:300]);return b
def metrics(values,errors=0):
 values=sorted(values);return {'samples':len(values),'p50_ms':round(statistics.median(values),2),'p95_ms':round(values[max(0,int(len(values)*.95)-1)],2),'p99_ms':round(values[max(0,int(len(values)*.99)-1)],2),'errors':errors}
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+DB+';Username=clubs','Logging__LogLevel__Default':'Warning'}
log=open('.infra/phase5-scale-api.log','w',encoding='utf-8')
def start():
 p=subprocess.Popen(['dotnet',os.environ.get('CLUBS_TEST_ASSEMBLY','server/bin/Debug/net10.0/Clubs.Api.dll'),'--urls',support.BASE],env=env,stdout=log,stderr=log)
 for _ in range(100):
  try:urllib.request.urlopen(support.BASE+'/health');return p
  except:time.sleep(.2)
 raise AssertionError('Scale API did not start')
process=start()
try:
 c=Client();assert c.login('admin@example.test')[0]==200;root=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(root);tag=uuid.uuid4().hex[:8];tenant=root['tenantId'];club=sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-006'");member=sql("SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='member@example.test'")
 category=sql(f"""SELECT "CategoryId" FROM "Memberships" WHERE "TenantId"='{tenant}' AND "Status"='Active' GROUP BY "CategoryId" HAVING count(DISTINCT "PersonId")=50000 LIMIT 1""");assert category,'Run the explicit earlier event-scale fixture first; do not target a production database.'
 print('Preparing 5,000 conversations, 50,000-message history and ten fictional inbox users...',flush=True)
 base=good(c,'/conversations','POST',{'orgId':club,'title':'Communication benchmark '+tag,'userIds':[member]});bid=base['id'];admin=base['ownerId']
 sql(f"""BEGIN;SET LOCAL statement_timeout='300s';
 INSERT INTO "Users" SELECT(jsonb_populate_record(NULL::"Users",to_jsonb(u)||jsonb_build_object('Id',md5('comm-user-{tag}-'||g.n)::uuid,'Email','comm-scale-{tag}-'||g.n||'@example.test','PlatformAdmin',false))).* FROM "Users" u CROSS JOIN generate_series(1,10)g(n) WHERE u."Id"='{admin}';
 INSERT INTO "UserProfiles" SELECT(jsonb_populate_record(NULL::"UserProfiles",to_jsonb(u)||jsonb_build_object('Id',md5('comm-user-{tag}-'||g.n)::uuid,'Name','Fictional inbox operator '||g.n))).* FROM "UserProfiles" u CROSS JOIN generate_series(1,10)g(n) WHERE u."Id"='{admin}';
 INSERT INTO "Assignments"("Id","UserId","TenantId","RoleId","OrgId") SELECT gen_random_uuid(),md5('comm-user-{tag}-'||g.n)::uuid,'{tenant}',a."RoleId",a."OrgId" FROM "Assignments" a CROSS JOIN generate_series(1,10)g(n) WHERE a."Id"='{root['id']}';
 INSERT INTO "Conversations" SELECT(jsonb_populate_record(NULL::"Conversations",to_jsonb(c)||jsonb_build_object('Id',md5('comm-inbox-{tag}-'||g.n)::uuid,'Title','Fictional inbox {tag} '||g.n,'Preview','A persisted benchmark inquiry','UpdatedAt',now()-g.n*interval '1 second'))).* FROM "Conversations" c CROSS JOIN generate_series(1,5000)g(n) WHERE c."Id"='{bid}';
 INSERT INTO "ConversationParticipants"("Id","TenantId","ConversationId","UserId","Staff","Muted","Active","ReadAt") SELECT gen_random_uuid(),'{tenant}',md5('comm-inbox-{tag}-'||g.n)::uuid,md5('comm-user-{tag}-'||u.n)::uuid,true,false,true,'0001-01-01T00:00:00Z' FROM generate_series(1,5000)g(n) CROSS JOIN generate_series(1,10)u(n);
 INSERT INTO "ConversationParticipants"("Id","TenantId","ConversationId","UserId","Staff","Muted","Active","ReadAt") SELECT gen_random_uuid(),'{tenant}',md5('comm-inbox-{tag}-'||g.n)::uuid,'{admin}',true,false,true,'0001-01-01T00:00:00Z' FROM generate_series(1,5000)g(n);
 INSERT INTO "CommunicationMessages"("Id","TenantId","OrgId","ConversationId","SenderId","Body","Subject","Direction","Internal","ClientKey","ProviderId","At") SELECT gen_random_uuid(),'{tenant}','{club}','{bid}','{admin}','Large-history fictional message '||g.n,'','Outbound',false,'history-{tag}-'||g.n,'',now()-g.n*interval '1 minute' FROM generate_series(1,50000)g(n);
 ANALYZE "Conversations";ANALYZE "ConversationParticipants";ANALYZE "CommunicationMessages";ANALYZE "Memberships";COMMIT;""")
 print('Linking the synthetic 50k audience to fictional in-app accounts for actual durable fan-out...',flush=True)
 sql(f"""BEGIN;SET LOCAL statement_timeout='300s';
 INSERT INTO "Users" SELECT(jsonb_populate_record(NULL::"Users",to_jsonb(u)||jsonb_build_object('Id',md5('comm-recipient-'||p."Id")::uuid,'Email','comm-recipient-'||p."Id"||'@example.test','PlatformAdmin',false))).* FROM "Users" u CROSS JOIN "Persons" p WHERE u."Id"='{admin}' AND p."UserId" IS NULL AND EXISTS(SELECT 1 FROM "Memberships" m WHERE m."PersonId"=p."Id" AND m."CategoryId"='{category}');
 INSERT INTO "UserProfiles" SELECT(jsonb_populate_record(NULL::"UserProfiles",to_jsonb(u)||jsonb_build_object('Id',md5('comm-recipient-'||p."Id")::uuid,'Name','Fictional campaign member'))).* FROM "UserProfiles" u CROSS JOIN "Persons" p WHERE u."Id"='{admin}' AND p."UserId" IS NULL AND EXISTS(SELECT 1 FROM "Memberships" m WHERE m."PersonId"=p."Id" AND m."CategoryId"='{category}');
 UPDATE "Persons" p SET "UserId"=md5('comm-recipient-'||p."Id")::uuid WHERE p."UserId" IS NULL AND EXISTS(SELECT 1 FROM "Memberships" m WHERE m."PersonId"=p."Id" AND m."CategoryId"='{category}');
 INSERT INTO "Assignments"("Id","UserId","TenantId","RoleId","OrgId","MembershipId") SELECT gen_random_uuid(),p."UserId",m."TenantId",r."Id",m."ClubId",m."Id" FROM "Memberships" m JOIN "Persons" p ON p."Id"=m."PersonId" JOIN "Roles" r ON r."TenantId"=m."TenantId" AND r."Name"='Club Member' WHERE m."CategoryId"='{category}' AND NOT EXISTS(SELECT 1 FROM "Assignments" a WHERE a."UserId"=p."UserId" AND a."MembershipId"=m."Id");
 ANALYZE "Users";ANALYZE "UserProfiles";ANALYZE "Persons";ANALYZE "Assignments";COMMIT;""")
 clients=[]
 for i in range(1,11):
  cli=Client();assert cli.login(f'comm-scale-{tag}-{i}@example.test')[0]==200;cli.activate(next(w for w in cli.req('/workspaces')[1] if w['tenantId']==tenant));clients.append(cli)
 # Two 50k snapshots submitted concurrently through the API. Submission does not dispatch.
 drafts=[good(c,'/campaigns','POST',{'orgId':root['orgId'],'title':f'50k communication batch {tag} {i}','body':'Hello {{member_name}}, an isolated benchmark update.','channel':'InApp','purpose':'Transactional','rules':json.dumps({'orgId':root['orgId'],'categoryId':category})}) for i in range(2)]
 print('Submitting two concurrent 50,000-recipient campaign snapshots...',flush=True)
 def submit(b):
  begin=time.perf_counter();r=good(c,'/campaigns/'+b['id']+'/status','POST',{'action':'Submit','version':b['version']});return r,(time.perf_counter()-begin)*1000
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:submitted=list(pool.map(submit,drafts))
 batches=[]
 for b,elapsed in submitted:
  count=int(sql(f"SELECT count(*) FROM \"CommunicationRecipients\" WHERE \"BroadcastId\"='{b['id']}'"));assert count==50000,count;batches.append({'recipients':count,'snapshot_ms':round(elapsed,2)})
  # Approval fixture only; independent real governance correctness is tested in phase5.py.
  sql(f"UPDATE \"CommunicationBroadcasts\" SET \"Status\"='Approved' WHERE \"Id\"='{b['id']}'")
  b=good(c,'/campaigns/'+b['id'])['broadcast'];good(c,'/campaigns/'+b['id']+'/status','POST',{'action':'Schedule','version':b['version']})
 ids=[b['id'] for b,_ in submitted];quoted=','.join("'"+x+"'" for x in ids)
 time.sleep(.8);before=sql(f'SELECT count(*) FROM "Mails" m JOIN "CommunicationMessages" x ON x."Id"=m."CommunicationId" WHERE x."BroadcastId" IN({quoted})');process.terminate();process.wait(timeout=15);process=start();after=sql(f'SELECT count(*) FROM "Mails" m JOIN "CommunicationMessages" x ON x."Id"=m."CommunicationId" WHERE x."BroadcastId" IN({quoted})');assert int(after)>=int(before)
 print('Restart recovered persisted jobs; measuring concurrent inbox users during campaign processing...',flush=True)
 # Warm the scoped inbox query for each real fixture user after restart; retain cold wave separately.
 cold=[]
 for client in clients:
  begin=time.perf_counter();good(client,'/conversations');cold.append((time.perf_counter()-begin)*1000)
 times=[];errors=[]
 def inbox(i):
  begin=time.perf_counter();code,b=clients[i%len(clients)].req('/communications/conversations');times.append((time.perf_counter()-begin)*1000)
  if code!=200:errors.append(code)
 with concurrent.futures.ThreadPoolExecutor(max_workers=10) as pool:list(pool.map(inbox,range(100)))
 assert not errors,errors
 reads={}
 for path in ['/conversations','/conversations/'+bid+'/messages','/analytics']:
  values=[]
  for i in range(51):
   begin=time.perf_counter();good(c,path)
   if i:values.append((time.perf_counter()-begin)*1000)
  reads[path.replace(bid,'{history}')]=metrics(values)
 begin=time.monotonic();deadline=begin+600
 while time.monotonic()<deadline:
  queued=int(sql(f'SELECT count(*) FROM "CommunicationRecipients" WHERE "BroadcastId" IN({quoted}) AND "QueuedCycle"=1'))
  if queued==100000:break
  time.sleep(.5)
 assert queued==100000,queued
 duplicates=int(sql(f'SELECT count(*) FROM(SELECT "BroadcastId","PersonId",count(*) FROM "CommunicationMessages" WHERE "BroadcastId" IN({quoted}) GROUP BY 1,2 HAVING count(*)>1)x'));assert duplicates==0
 counts={table:int(sql('SELECT count(*) FROM "'+table+'"')) for table in ['Persons','Orgs','Conversations','ConversationParticipants','CommunicationMessages','CommunicationRecipients','Mails','Users']}
 plans={}
 for name,query in {'inbox':f"SELECT \"Id\" FROM \"Conversations\" WHERE \"TenantId\"='{tenant}' ORDER BY \"UpdatedAt\" DESC,\"Id\" DESC LIMIT 30",'history':f"SELECT \"Id\",\"Body\" FROM \"CommunicationMessages\" WHERE \"ConversationId\"='{bid}' ORDER BY \"At\" DESC,\"Id\" DESC LIMIT 50",'recipient':f"SELECT \"Id\" FROM \"CommunicationRecipients\" WHERE \"BroadcastId\"='{ids[0]}' AND \"QueuedCycle\"<2 ORDER BY \"Id\" LIMIT 250"}.items():plans[name]=json.loads(sql('EXPLAIN(ANALYZE,BUFFERS,TIMING OFF,FORMAT JSON) '+query))
 report={'counts':counts,'campaigns':batches,'simultaneous_campaigns':2,'restart':{'outbox_before':int(before),'outbox_after':int(after),'recipient_jobs_queued':queued,'duplicate_messages':duplicates},'restart_first_inbox_reads':metrics(cold),'concurrent_inbox':{'users':10,'workers':10,**metrics(times,len(errors))},'api':reads,'conditions':'Isolated PostgreSQL 17.6 clone; 100k+ people/10k+ organizations. 5,000 additional conversations, 50,000-message history, ten fictional authenticated inbox operators. Two 50,000-recipient snapshots submitted concurrently through real API; approval state is explicit benchmark SQL only. Durable worker is stopped/restarted during fan-out; 100,000 recipient jobs resume without duplicate messages. Synthetic audience members have fictional linked accounts and scoped memberships. In-app fan-out creates real persisted messages/outbox records; no external channel is enabled. Inbox concurrency: 100 requests, ten users/threads during processing. Sequential APIs: 50 warm samples after one warmup. Local shared machine; no external provider or production throughput claim.'}
 pathlib.Path('test-results/phase5-scale.json').write_text(json.dumps(report,indent=2));pathlib.Path('test-results/phase5-explain.json').write_text(json.dumps(plans,indent=2));print(json.dumps(report,indent=2),flush=True)
finally:process.terminate();process.wait(timeout=15);log.close()
