"""Phase 4 real PostgreSQL event lifecycle, security and concurrency checks."""
from support import Client,sql
import uuid,datetime,time,json,pathlib,concurrent.futures,statistics
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name, '' if ok else str(data)[:500],flush=True);pathlib.Path('test-results/phase4.json').write_text(json.dumps(results,indent=2));assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,b=c.req(p,m,d);assert code==status,(p,code,b);return b
def personal_invitations(c):
 rows=[];path='/my/invitations'
 while True:
  page=good(c,path);rows.extend(page)
  if len(page)<50:return rows
  path='/my/invitations?after='+page[-1]['id']
def at(hours=0):return (datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=hours)).isoformat()
sql('UPDATE "EventInvitations" SET "Status"=\'Cancelled\' WHERE "Status"=\'Queued\' AND "EventId" IN(SELECT "Id" FROM "Events" WHERE "Title" LIKE \'Verified community gathering %\' OR "Title" LIKE \'Capacity race %\')')
tag=uuid.uuid4().hex[:8];admin=Client();good(admin,'/auth/login','POST',{'email':'admin@example.test','password':__import__('support').PASSWORD});root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
review=Client();review.login('secretary@example.test');review.activate(next(w for w in good(review,'/workspaces') if w['role']=='Association Super Admin' and w['orgId']==root['orgId']))
member=Client();member.login('member@example.test');club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');member.activate(next(w for w in good(member,'/workspaces') if w['role']=='Club Member' and w['orgId']==club));person=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');district=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-004\'');types=good(admin,'/event-types');kind=types[0]['id']
base={'orgId':club,'typeId':kind,'title':'Verified community gathering '+tag,'description':'A real PostgreSQL event workflow fixture','startsAt':at(-1),'endsAt':at(5),'registrationStartsAt':at(-24),'registrationEndsAt':at(3),'timeZone':'Asia/Kolkata','venue':'Community learning centre','capacity':3,'allowGuests':True,'maxCompanions':1,'visibility':'Restricted'}
def create(**kwargs):return good(admin,'/events','POST',{**base,**kwargs})
def status(e,action):return good(admin,'/events/'+e['id']+'/status','POST',{'action':action,'version':e['version'],'reason':'Integration-tested event transition'})
def approve(e):
 e=status(e,'Submit');r=good(admin,'/governance/requests/'+e['approvalRequestId'])['request'];check('event requester cannot self-approve',admin.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'','version':r['version']})[0]==403);good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent event review','version':r['version']});return good(admin,'/events/'+e['id'])['eventInfo']
e=create();eid=e['id'];check('club event draft created',e['approval']=='Draft' and e['publication']=='Draft');check('unauthorized event creation rejected',member.req('/events','POST',base)[0]==403)
check('invalid date range rejected',admin.req('/events','POST',{**base,'endsAt':at(-2)})[0]==400);check('paid booking cannot fabricate payment',admin.req('/events','POST',{**base,'paid':True})[0]==409)
district_event=create(orgId=district,title='District conference '+tag);check('district event ownership stored',district_event['orgId']==district)
check('restricted draft hidden from member',member.req('/events/'+eid)[0]==403)
check('cannot publish without approval',admin.req('/events/'+eid+'/status','POST',{'action':'Publish','version':e['version']})[0]==409)
session=good(admin,'/events/'+eid+'/sessions','POST',{'title':'Leadership lab','speaker':'Speaker A','room':'Room A','startsAt':at(-.5),'endsAt':at(1),'capacity':2});check('session agenda persisted',session['eventId']==eid)
check('room conflict rejected',admin.req('/events/'+eid+'/sessions','POST',{'title':'Conflicting lab','speaker':'Speaker B','room':'Room A','startsAt':at(0),'endsAt':at(2),'capacity':2})[0]==409)
good(admin,'/events/'+eid+'/audience','PUT',{'rules':[{'orgId':district,'status':'Active'},{'personId':person,'status':'Active'}]});aud=good(admin,'/events/'+eid+'/audience');check('overlapping audience is deduplicated',aud['total']==int(sql(f'''WITH RECURSIVE tree AS(SELECT "Id" FROM "Orgs" WHERE "Id"='{district}' UNION ALL SELECT o."Id" FROM "Orgs" o JOIN tree t ON o."ParentId"=t."Id") SELECT count(DISTINCT "PersonId") FROM "Memberships" WHERE "TenantId"='{root['tenantId']}' AND "ClubId" IN(SELECT "Id" FROM tree) AND "Status"='Active' AND ("EndsAt" IS NULL OR "EndsAt">now())''')))
good(admin,'/events/'+eid+'/audience','PUT',{'rules':[{'personId':person,'status':'Active'},{'personId':person,'status':'Active'}]})
e=approve(e);check('shared governance engine approves event',e['approval']=='Approved');e=status(e,'Publish');check('approved event published',e['publication']=='Published')
check('eligible member can view published event',good(member,'/events/'+eid)['eventInfo']['id']==eid);check('eligible organization audience discovered',any(x['id']==eid for x in good(member,'/events?size=100&search='+tag+'&from='+__import__('urllib.parse',fromlist=['quote']).quote(at(-24))+'&to='+__import__('urllib.parse',fromlist=['quote']).quote(at(24)))['items']))
first=good(admin,'/events/'+eid+'/invitations','POST',{'message':'Invitation test','guests':[{'name':'Consenting Guest','email':'event-'+tag+'@example.test','consent':True}]});again=good(admin,'/events/'+eid+'/invitations','POST',{});check('duplicate invitation recipients suppressed',first['queued']>0 and again['queued']==0)
check('verified member and guest email alias deduplicate to one person',good(admin,'/events/'+eid+'/invitations','POST',{'guests':[{'name':'Maya alias','email':'member@example.test','consent':True}]})['queued']==0)
check('audience frozen after publication',admin.req('/events/'+eid+'/audience','PUT',{'rules':[]})[0]==409)
member_inv=sql(f'SELECT "Id" FROM "EventInvitations" WHERE "EventId"=\'{eid}\' AND "PersonId"=\'{person}\'');check('personal invitations list includes event',any(i['eventId']==eid for i in personal_invitations(member)))
r=good(member,'/events/'+eid+'/rsvps','POST',{'response':'Going'});check('RSVP does not reserve a seat',r['seatReserved']==False and sql(f'SELECT count(*) FROM "EventRegistrations" WHERE "EventId"=\'{eid}\'')=='0');good(member,'/events/'+eid+'/rsvps','POST',{'response':'Maybe'});check('RSVP change preserves response history',int(sql(f'SELECT count(*) FROM "EventRsvpHistory" WHERE "InvitationId"=\'{member_inv}\''))==2)
reg=good(member,'/events/'+eid+'/registrations','POST',{'companions':0,'guestNames':[],'sessionIds':[session['id']]});check('eligible member registration confirmed',reg['status']=='Confirmed');check('registration idempotency prevents duplicate seat',member.req('/events/'+eid+'/registrations','POST',{})[0]==409)
ticket=good(member,'/events/'+eid+'/tickets/'+reg['id'],'POST',{});check('opaque digital ticket generated',len(ticket['token'])==64 and '@' not in ticket['token']);check('anonymous directory remains denied',Client().req('/members')[0]==401)
check('unauthorized check-in denied',member.req('/events/'+eid+'/checkins','POST',{'token':ticket['token']})[0]==403)
verify=good(admin,'/events/'+eid+'/checkins/verify','POST',{'token':ticket['token']});check('QR verification returns valid registration',verify['state']=='Valid')
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
 codes=list(pool.map(lambda _:admin.req('/events/'+eid+'/checkins','POST',{'token':ticket['token'],'device':'Device '+str(_),'entryPoint':'North'})[0],range(8)))
check('multiple devices create one attendance record',codes.count(200)==1 and codes.count(409)==7,codes);check('duplicate QR check-in rejected',good(admin,'/events/'+eid+'/checkins/verify','POST',{'token':ticket['token']})['state']=='Already Checked In')
good(admin,'/events/'+eid+'/checkins','POST',{'token':ticket['token'],'sessionId':session['id']});check('session attendance separate from event',sql(f'SELECT count(*) FROM "EventCheckins" WHERE "RegistrationId"=\'{reg["id"]}\'')=='2')
code,pdf,_=member.request('/events/'+eid+'/passes/'+reg['id']+'/pdf','POST',b'{}',{'Content-Type':'application/json'});check('downloadable PDF pass contains actual PDF',code==200 and pdf.startswith(b'%PDF-') and b'%%EOF' in pdf)
good(admin,'/events/'+eid+'/tickets/'+reg['id']+'/revoke','POST',{},204);check('revoked ticket rejected',good(admin,'/events/'+eid+'/checkins/verify','POST',{'token':ticket['token']})['state']=='Revoked Ticket');good(member,'/events/'+eid+'/tickets/'+reg['id']+'/reissue','POST',{});new=good(member,'/events/'+eid+'/tickets/'+reg['id'],'POST',{});check('reissued pass has a new token',new['token']!=ticket['token'])
other=create(title='Capacity race '+tag,capacity=2,maxCompanions=0,visibility='Public');good(admin,'/events/'+other['id']+'/audience','PUT',{'rules':[{'personId':person,'status':'Active'}]});other=status(approve(other),'Publish');oid=other['id']
# Secure invitation tokens are recovered from the existing development outbox only, never database plaintext.
good(admin,'/events/'+oid+'/invitations','POST',{'guests':[{'name':'Race Guest '+str(i),'email':f'race-{tag}-{i}@example.test','consent':True} for i in range(8)]})
deadline=time.monotonic()+15;tokens=[]
while time.monotonic()<deadline:
 mails=good(admin,'/development/mail');tokens=[m['link'].split('token=')[1] for m in mails if m.get('link','').startswith('/event-invitation?token=') and 'Race' not in m.get('subject','') and any(f'race-{tag}-{i}@example.test'==m.get('email') for i in range(8))]
 if len(tokens)==8:break
 time.sleep(.3)
assert len(tokens)==8,('guest mail links',len(tokens),mails[-1:] if mails else [])
guests=[Client() for _ in tokens];assert all(good(c,'/event-invitations/redeem','POST',{'token':t})['eventInfo']['id']==oid for c,t in zip(guests,tokens));check('secure guest links redeem only intended event',True)
lat=[]
def register(pair):
 c,t=pair;begin=time.perf_counter();code,body=c.req('/events/'+oid+'/registrations','POST',{'token':t,'companions':0,'guestNames':[]});lat.append((time.perf_counter()-begin)*1000);return code,body
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool: regs=list(pool.map(register,zip(guests,tokens)))
check('concurrent registration does not oversell',sum(b.get('status')=='Confirmed' for code,b in regs)==2 and all(code==200 for code,b in regs),regs);check('capacity overflow joins waitlist',sum(b.get('status')=='Waitlisted' for _,b in regs)==6)
idx=next(i for i,(_,b) in enumerate(regs) if b['status']=='Confirmed');good(guests[idx],'/events/'+oid+'/registrations/'+regs[idx][1]['id']+'/cancel','POST',{'token':tokens[idx]});check('cancellation promotes earliest waitlisted guest',sql(f'SELECT count(*) FROM "EventRegistrations" WHERE "EventId"=\'{oid}\' AND "Status"=\'Confirmed\'')=='2')
check('guest registration never creates association membership',sql(f'SELECT count(*) FROM "Persons" WHERE "DisplayName" LIKE \'Race Guest%\'')=='0')
check('guest token cannot access a different event',guests[0].req('/events/'+eid+'/registrations','POST',{'token':tokens[0]})[0]==403)
sql(f'UPDATE "EventInvitations" SET "ExpiresAt"=now()-interval \'1 second\' WHERE "TokenHash"=\'{__import__("hashlib").sha256(tokens[0].encode()).hexdigest().upper()}\'');check('expired invitation rejected',guests[0].req('/event-invitations/redeem','POST',{'token':tokens[0]})[0]==410)
# Dedicated event staff cannot edit owner/audience or other events.
good(admin,'/events/'+eid+'/team','POST',{'personId':person,'role':'Welcome volunteer','capability':'Checkin','startsAt':at(-2),'endsAt':at(6),'responsibilities':'Welcome participants'});check('event operator gains check-in only on assigned event',good(member,'/events/'+eid+'/checkins/verify','POST',{'token':new['token']})['state']=='Already Checked In');check('volunteer cannot manage registrations or event ownership',member.req('/events/'+eid,'PUT',base)[0]==403)
check('event duty grants do not affect other event',member.req('/events/'+oid+'/checkins','POST',{'registrationId':regs[1][1]['id']})[0]==403)
feedback=good(admin,'/events/'+eid+'/feedback-form','PUT',{'questions':'[{"key":"overall","label":"Overall","type":"Rating","required":true}]','anonymous':True,'deadline':at(24)});e=good(admin,'/events/'+eid)['eventInfo'];e=status(e,'Complete');good(member,'/events/'+eid+'/feedback/'+reg['id'],'POST',{'answers':'{"overall":5}'});check('attendee feedback persisted',sql(f'SELECT count(*) FROM "EventFeedbackResponses" WHERE "EventId"=\'{eid}\'')=='1');check('anonymous response has no registration link',sql(f'SELECT "RegistrationId" IS NULL FROM "EventFeedbackResponses" WHERE "EventId"=\'{eid}\'')=='t');check('small feedback group suppressed',good(admin,'/events/'+eid+'/feedback')['suppressed']);check('anonymous feedback export denied',admin.req('/events/'+eid+'/feedback/export')[0]==403)
analytics=good(admin,'/events/'+eid+'/analytics');check('attendance analytics use actual event attendance',analytics['attended']==1 and analytics['confirmed']==1 and analytics['attendanceRate']==100);check('district dashboard aggregates actual event records',good(admin,'/events-dashboard')['eventsCreated']>=3)
second=good(admin,'/workspaces');otherws=next(x for x in second if x['role']=='Association Super Admin' and x['tenantId']!=root['tenantId']);admin.activate(otherws);check('cross-tenant modification denied',admin.req('/events/'+eid+'/status','POST',{'action':'Cancel','version':e['version'],'reason':'Forbidden cross tenant mutation'})[0]==403);admin.activate(root)
rejected=create(title='Rejected event '+tag);rejected=status(rejected,'Submit');request=good(admin,'/governance/requests/'+rejected['approvalRequestId'])['request'];good(review,'/governance/requests/'+request['id']+'/decisions','POST',{'decision':'Reject','comment':'Needs revision','version':request['version']});check('event rejection blocks publication',good(admin,'/events/'+rejected['id'])['eventInfo']['approval']=='Rejected')
e=good(admin,'/events/'+oid)['eventInfo'];e=status(e,'Cancel');check('cancelled event cannot register',guests[1].req('/events/'+oid+'/registrations','POST',{'token':tokens[1]})[0]==409);check('cancellation revokes all event tickets',sql(f'SELECT count(*) FROM "EventTickets" WHERE "EventId"=\'{oid}\' AND NOT "Revoked"')=='0')
check('event UTC and original time zone preserved',good(admin,'/events/'+eid)['eventInfo']['timeZone']=='Asia/Kolkata')
pathlib.Path('test-results/phase4-concurrency.json').write_text(json.dumps({'registration_concurrency':8,'checkin_devices':8,'samples':len(lat),'p50_ms':statistics.median(lat),'p95_ms':max(lat),'p99_ms':max(lat),'errors':0,'conditions':'Eight concurrent PostgreSQL registrations for two seats; eight simultaneous same-pass check-in writes. Small correctness load; not production capacity.'},indent=2))
print(len(results),'Phase 4 checks passed')
