"""Isolated event-scale fixture, real HTTP fan-out and concurrent registration/check-in measurements."""
from support import Client,sql
import pathlib,uuid,datetime,time,json,statistics,concurrent.futures,hashlib,os
DB='clubs_governance_scale'
def run(q):return sql(q,DB)
def at(h=0):return (datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=h)).isoformat()
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code==200,(p,code,str(b)[:300]);return b
c=Client();assert c.login('admin@example.test')[0]==200;w=next(x for x in good(c,'/workspaces') if x['role']=='Association Super Admin' and x['association'].startswith('Global'));c.activate(w)
tenant=w['tenantId'];root=w['orgId'];typeid=good(c,'/event-types')[0]['id'];tag=uuid.uuid4().hex[:8];category=str(uuid.uuid4());event=str(uuid.uuid4());convention=str(uuid.uuid4())
print('Preparing isolated synthetic event scale fixture...',flush=True)
run(f'''BEGIN; SET LOCAL statement_timeout='300s';INSERT INTO "MembershipCategories" SELECT(jsonb_populate_record(NULL::"MembershipCategories",to_jsonb(c)||jsonb_build_object('Id','{category}','Name','Event benchmark {tag}','Code','EV-{tag}'))).* FROM "MembershipCategories" c WHERE "TenantId"='{tenant}' LIMIT 1;
 UPDATE "Memberships" SET "CategoryId"='{category}' WHERE "Id" IN(SELECT DISTINCT ON("PersonId") "Id" FROM "Memberships" WHERE "TenantId"='{tenant}' AND "Status"='Active' ORDER BY "PersonId","Id" LIMIT 50000);
 INSERT INTO "Events" ("Id","TenantId","OrgId","TypeId","CreatorId","Title","Description","TimeZone","Language","StartsAt","EndsAt","VenueType","Venue","OnlineUrl","Contact","Visibility","Approval","Publication","Lifecycle","RegistrationEnabled","RsvpRequired","RegistrationStartsAt","RegistrationEndsAt","RsvpDeadline","Capacity","Waitlist","WaitlistPolicy","OfferMinutes","AllowGuests","MaxCompanions","RegistrationApproval","Paid","CancellationPolicy","Fields","ReminderSent","CompletionSent","Version","CreatedAt") VALUES ('{event}','{tenant}','{root}','{typeid}',(SELECT "Id" FROM "Users" WHERE "Email"='admin@example.test'),'50k Invitation Benchmark {tag}','','UTC','en',now()+interval '2 hours',now()+interval '8 hours','Physical','Benchmark convention hall','','','Restricted','Approved','Published','Scheduled',true,true,now()-interval '1 day',now()+interval '6 hours',now()+interval '6 hours',20000,true,'Allocate',30,true,0,false,false,'Before close','[]',false,false,1,now());
 INSERT INTO "EventAudiences"("Id","TenantId","EventId","OrgId","CategoryId","City","Country","Status","RegistrationStatus") VALUES(gen_random_uuid(),'{tenant}','{event}','{root}','{category}','','','Active','');
 INSERT INTO "Events" SELECT(jsonb_populate_record(NULL::"Events",to_jsonb(e)||jsonb_build_object('Id','{convention}','Title','10k Convention {tag}','StartsAt',now()-interval '1 hour','Visibility','Public','Capacity',20000))).* FROM "Events" e WHERE "Id"='{event}';
 INSERT INTO "EventRegistrations"("Id","TenantId","EventId","PersonId","MembershipId","ParticipantKey","Name","Email","Category","Status","Companions","GuestNames","Responses","Accessibility","Dietary","Emergency","CreatedAt","FeedbackSubmitted","Version") SELECT md5('event-scale-{tag}-'||m."Id")::uuid,m."TenantId",'{convention}',m."PersonId",m."Id",'person:'||m."PersonId",p."DisplayName",'','Member','Confirmed',0,'[]','{{}}','','','',now(),false,1 FROM "Memberships" m JOIN "Persons" p ON p."Id"=m."PersonId" WHERE m."CategoryId"='{category}' LIMIT 10000;
 ANALYZE "Memberships";ANALYZE "EventAudiences";ANALYZE "EventInvitations";ANALYZE "Events";ANALYZE "EventRegistrations";COMMIT;''')
print('Fixture ready; queuing 50,000 deduplicated invitations through API...',flush=True)
# The same event remains writable while its 50k-recipient batch runs.
run(f'''UPDATE "Events" SET "Visibility"='Public',"StartsAt"=now()-interval '1 hour' WHERE "Id"='{event}';''')
reg=good(c,'/events/'+event+'/registrations','POST',{})
ticket=good(c,'/events/'+event+'/tickets/'+reg['id'],'POST',{})
def enqueue():
 begin=time.perf_counter();batch=good(c,'/events/'+event+'/invitations','POST',{});return batch,(time.perf_counter()-begin)*1000
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
 future=pool.submit(enqueue)
 time.sleep(.2)
 overlap_begin=time.perf_counter()
 good(c,'/events/'+event+'/checkins','POST',{'token':ticket['token'],'device':'During bulk invitation enqueue'})
 overlap_ms=(time.perf_counter()-overlap_begin)*1000
 overlapped=not future.done()
 batch,batch_ms=future.result()
assert batch['queued']==50000,batch
assert overlapped and overlap_ms<3000,('bulk blocked check-in',overlapped,overlap_ms)
begin=time.perf_counter();retry=good(c,'/events/'+event+'/invitations','POST',{});retry_ms=(time.perf_counter()-begin)*1000;assert retry['queued']==0
print('Invitation batch persisted; exercising concurrent bookings...',flush=True)
# Separate event with 50 seats and 100 consenting guests; email-link delivery uses the existing outbox.
race=good(c,'/events','POST',{'orgId':root,'typeId':typeid,'title':'Registration load '+tag,'startsAt':at(-1),'endsAt':at(8),'registrationStartsAt':at(-24),'registrationEndsAt':at(6),'venue':'Scale registration hall','visibility':'Public','capacity':50,'allowGuests':True,'maxCompanions':0})
# This is a performance fixture, not an approval-engine test (covered by phase4.py).
run(f'''UPDATE "Events" SET "Approval"='Approved',"Publication"='Published' WHERE "Id"='{race['id']}';''')
good(c,'/events/'+race['id']+'/invitations','POST',{'guests':[{'name':f'Load Guest {i}','email':f'load-{tag}-{i}@example.test','consent':True} for i in range(100)]})
deadline=time.monotonic()+30;tokens=[]
while time.monotonic()<deadline:
 tokens=[x['link'].split('token=')[1] for x in good(c,'/development/mail') if x.get('email','').startswith('load-'+tag+'-')]
 if len(tokens)==100:break
 time.sleep(.5)
assert len(tokens)==100,len(tokens)
clients=[Client() for _ in tokens];times=[]
def register(pair):
 client,token=pair;begin=time.perf_counter();code,b=client.req('/events/'+race['id']+'/registrations','POST',{'token':token,'guestNames':[]});times.append((time.perf_counter()-begin)*1000);return code,b.get('status')
with concurrent.futures.ThreadPoolExecutor(max_workers=20) as pool:statuses=list(pool.map(register,zip(clients,tokens)))
assert statuses.count((200,'Confirmed'))==50 and statuses.count((200,'Waitlisted'))==50,statuses
# 100 actual opaque tickets for existing convention registrations, distinct devices plus replay contention.
ids=run(f'''SELECT "Id" FROM "EventRegistrations" WHERE "EventId"='{convention}' ORDER BY "Id" LIMIT 100''').splitlines();ticket_tokens=[uuid.uuid4().hex+uuid.uuid4().hex for _ in ids]
values=','.join(f"(gen_random_uuid(),'{tenant}','{convention}','{rid}','{hashlib.sha256(t.encode()).hexdigest().upper()}','fixture-token-not-for-download',false,now())" for rid,t in zip(ids,ticket_tokens));run('INSERT INTO "EventTickets"("Id","TenantId","EventId","RegistrationId","TokenHash","ProtectedToken","Revoked","IssuedAt") VALUES '+values)
checktimes=[]
def checkin(pair):
 i,t=pair;begin=time.perf_counter();code,b=c.req('/events/'+convention+'/checkins','POST',{'token':t,'device':f'Device {i}','entryPoint':'Benchmark'});checktimes.append((time.perf_counter()-begin)*1000);return code
with concurrent.futures.ThreadPoolExecutor(max_workers=20) as pool:codes=list(pool.map(checkin,enumerate(ticket_tokens)))
assert codes.count(200)==100,codes
with concurrent.futures.ThreadPoolExecutor(max_workers=20) as pool:replays=list(pool.map(lambda _:c.req('/events/'+convention+'/checkins','POST',{'token':ticket_tokens[0]})[0],range(20)))
assert all(x==409 for x in replays)
def metrics(v,errors=0):
 s=sorted(v);return {'samples':len(s),'p50_ms':round(statistics.median(s),2),'p95_ms':round(s[max(0,int(len(s)*.95)-1)],2),'p99_ms':round(s[max(0,int(len(s)*.99)-1)],2),'errors':errors,'error_rate':errors/len(s)}
api={}
for path in ['/events?size=30','/events/'+convention+'/registrations','/events/'+convention+'/analytics']:
 v=[]
 for i in range(51):
  begin=time.perf_counter();good(c,path)
  if i:v.append((time.perf_counter()-begin)*1000)
 api[path.replace(convention,'{convention}')]=metrics(v)
plans={}
for name,q in {'calendar':f'''SELECT "Id","Title","StartsAt" FROM "Events" WHERE "TenantId"='{tenant}' AND "StartsAt">now()-interval '1 month' AND "StartsAt"<now()+interval '2 months' ORDER BY "StartsAt","Id" LIMIT 30''','registrations':f'''SELECT "Id","Name","Status" FROM "EventRegistrations" WHERE "EventId"='{convention}' ORDER BY "Id" LIMIT 50''','invitation_recipient':f'''SELECT "Id","Status" FROM "EventInvitations" WHERE "EventId"='{event}' ORDER BY "Id" LIMIT 50''','checkin_lookup':f'''SELECT "Id","Revoked" FROM "EventTickets" WHERE "TokenHash"='{hashlib.sha256(ticket_tokens[0].encode()).hexdigest().upper()}' '''}.items():plans[name]=json.loads(run('EXPLAIN(ANALYZE,BUFFERS,TIMING OFF,FORMAT JSON) '+q))
counts={name:int(run(f'SELECT count(*) FROM "{name}"')) for name in ['Persons','Memberships','Orgs','Events','EventInvitations','EventRegistrations','EventCheckins']}
report={'counts':counts,'invitation_batch':{'recipients':batch['queued'],'milliseconds':round(batch_ms,2),'recipients_per_second':round(batch['queued']/(batch_ms/1000),2),'idempotent_retry_ms':round(retry_ms,2),'duplicates':retry['queued'],'samples':1,'same_event_checkin_during_batch_ms':round(overlap_ms,2),'checkin_completed_before_batch':overlapped},'registration_load':{'concurrent_workers':20,'confirmed':50,'waitlisted':50,**metrics(times)},'checkin_load':{'devices':20,'distinct_passes':100,'duplicate_replays_rejected':20,**metrics(checktimes)},'api':api,'conditions':'Isolated PostgreSQL 17.6 synthetic clone with 100k+ people and 10k+ clubs. 10,000 confirmed convention registrations. Actual API 50,000 invitation fan-out, 100 guest registrations through 20 threads, 100 distinct check-ins through 20 threads and 20 duplicate replays. Sequential reads: 50 warm samples. Event lifecycle setup via explicit benchmark SQL; approval correctness is independently API-tested. No provider delivery or production capacity claim. Queued recipients are persisted, not claimed sent.'}
pathlib.Path('test-results/phase4-scale.json').write_text(json.dumps(report,indent=2));pathlib.Path('test-results/phase4-explain.json').write_text(json.dumps(plans,indent=2));print(json.dumps(report,indent=2))
