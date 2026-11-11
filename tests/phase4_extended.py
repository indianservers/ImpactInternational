"""Event deadlines, approvals, consent, files, waitlist offers and effective staff authority."""
from support import Client,sql
import uuid,json,pathlib,datetime,time,hashlib
results=[]
def check(n,ok,d=None):
 results.append({'test':n,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+n,flush=True);pathlib.Path('test-results/phase4-extended.json').write_text(json.dumps(results,indent=2));assert ok,(n,d)
def good(c,p,m='GET',d=None,status=200):
 code,b=c.req(p,m,d);assert code==status,(p,code,str(b)[:300]);return b
def at(h=0):return (datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=h)).isoformat()
tag=uuid.uuid4().hex[:8];admin=Client();admin.login('admin@example.test');root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root);review=Client();review.login('secretary@example.test');review.activate(next(w for w in good(review,'/workspaces') if w['role']=='Association Super Admin' and w['orgId']==root['orgId']));member=Client();member.login('member@example.test');club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');member.activate(next(w for w in good(member,'/workspaces') if w['role']=='Club Member' and w['orgId']==club));person=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');kind=good(admin,'/event-types')[0]['id']
base={'orgId':club,'typeId':kind,'title':'Extended event '+tag,'startsAt':at(-1),'endsAt':at(4),'registrationStartsAt':at(-24),'registrationEndsAt':at(3),'venue':'Community venue','capacity':1,'visibility':'Public','allowGuests':True,'waitlistPolicy':'Offer','offerMinutes':1}
def create(**kw):return good(admin,'/events','POST',{**base,**kw})
def approve(e):
 e=good(admin,'/events/'+e['id']+'/status','POST',{'action':'Submit','version':e['version']});r=good(admin,'/governance/requests/'+e['approvalRequestId'])['request'];good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent event check','version':r['version']});e=good(admin,'/events/'+e['id'])['eventInfo'];return good(admin,'/events/'+e['id']+'/status','POST',{'action':'Publish','version':e['version']})
e=approve(create());eid=e['id'];good(admin,'/events/'+eid+'/invitations','POST',{'guests':[{'name':'Offer guest '+str(i),'email':f'offer-{tag}-{i}@example.test','consent':True} for i in range(3)]});deadline=time.monotonic()+15;tokens=[]
while time.monotonic()<deadline:
 tokens=[m['link'].split('token=')[1] for m in good(admin,'/development/mail') if m.get('email','').startswith('offer-'+tag+'-')]
 if len(tokens)==3:break
 time.sleep(.3)
assert len(tokens)==3
clients=[Client() for _ in tokens];regs=[good(c,'/events/'+eid+'/registrations','POST',{'token':t}) for c,t in zip(clients,tokens)]
good(clients[0],'/events/'+eid+'/registrations/'+regs[0]['id']+'/cancel','POST',{'token':tokens[0]});offered=sql(f'SELECT "Id" FROM "EventRegistrations" WHERE "EventId"=\'{eid}\' AND "Status"=\'Offered\'');check('waitlist promotion reserves an expiring offer',bool(offered));idx=next(i for i,r in enumerate(regs) if r['id']==offered);good(clients[idx],'/events/'+eid+'/registrations/'+offered+'/accept-offer','POST',{'token':tokens[idx]});check('valid seat offer can be accepted',sql(f'SELECT "Status" FROM "EventRegistrations" WHERE "Id"=\'{offered}\'')=='Confirmed')
good(clients[idx],'/events/'+eid+'/registrations/'+offered+'/cancel','POST',{'token':tokens[idx]});last=sql(f'SELECT "Id" FROM "EventRegistrations" WHERE "EventId"=\'{eid}\' AND "Status"=\'Offered\'');sql(f'UPDATE "EventRegistrations" SET "OfferExpiresAt"=now()-interval \'1 second\' WHERE "Id"=\'{last}\'');j=next(i for i,r in enumerate(regs) if r['id']==last);check('expired offer cannot confirm a seat',clients[j].req('/events/'+eid+'/registrations/'+last+'/accept-offer','POST',{'token':tokens[j]})[0]==409)
# Guest feedback remains possible after the event; invitation and admission expiry are separate.
guest_event=approve(create(title='Post-event guest feedback '+tag,capacity=2));gid=guest_event['id'];guest_email='feedback-'+tag+'@example.test';good(admin,'/events/'+gid+'/guest-link','POST',{'name':'Post-event guest','email':guest_email,'consent':True});deadline=time.monotonic()+12;gtoken=None
while time.monotonic()<deadline:
 matches=[m['link'].split('token=')[1] for m in good(admin,'/development/mail') if m.get('email')==guest_email]
 if matches:gtoken=matches[0];break
 time.sleep(.3)
assert gtoken
guest=Client();gr=good(guest,'/events/'+gid+'/registrations','POST',{'token':gtoken});good(admin,'/events/'+gid+'/checkins','POST',{'registrationId':gr['id']});sql(f'UPDATE "Events" SET "StartsAt"=now()-interval \'2 hours\',"EndsAt"=now()-interval \'2 minutes\',"RegistrationEndsAt"=now()-interval \'3 minutes\' WHERE "Id"=\'{gid}\'');ge=good(admin,'/events/'+gid)['eventInfo'];good(admin,'/events/'+gid+'/status','POST',{'action':'Complete','version':ge['version']});good(admin,'/events/'+gid+'/feedback-form','PUT',{'questions':'[{"key":"rating","label":"Overall","type":"Rating"}]','anonymous':True,'deadline':at(24)});check('guest bearer link remains valid for post-event feedback',good(guest,'/event-invitations/redeem','POST',{'token':gtoken})['eventInfo']['lifecycle']=='Completed');good(guest,'/events/'+gid+'/feedback/'+gr['id'],'POST',{'token':gtoken,'answers':'{"rating":5}'});check('attended guest can submit anonymous post-event feedback',sql(f'SELECT "RegistrationId" IS NULL FROM "EventFeedbackResponses" WHERE "EventId"=\'{gid}\'')=='t')
# Approval is a shared governance decision; pending registration occupies no seat.
approved_event=approve(create(title='Reviewed registration '+tag,registrationApproval=True,capacity=5));aid=approved_event['id'];r=good(member,'/events/'+aid+'/registrations','POST',{});check('reviewed registration awaits approval without a seat',r['status']=='Awaiting Approval' and good(admin,'/events/'+aid+'/analytics')['seats']==0);request=good(admin,'/governance/requests/'+r['approvalRequestId'])['request'];good(review,'/governance/requests/'+request['id']+'/decisions','POST',{'decision':'Approve','comment':'Eligible participant','version':request['version']});check('shared engine confirms reviewed registration',sql(f'SELECT "Status" FROM "EventRegistrations" WHERE "Id"=\'{r["id"]}\'')=='Confirmed')
# Scoped staff expiry checked immediately, independent of background cleanup.
good(admin,'/events/'+aid+'/team','POST',{'personId':person,'role':'Event helper','capability':'Checkin','startsAt':at(-2),'endsAt':at(5)});sql(f'UPDATE "EventStaff" SET "EndsAt"=now()-interval \'1 second\' WHERE "EventId"=\'{aid}\' AND "PersonId"=\'{person}\'');check('expired event duty cannot check in',member.req('/events/'+aid+'/checkins','POST',{'registrationId':r['id']})[0]==403)
# Publication changes require the same independent approval trail.
e=good(admin,'/events/'+aid)['eventInfo'];proposal=good(admin,'/events/'+aid+'/amend','POST',{'eventId':aid,'title':e['title'],'description':'Reviewed updated details','venue':'Approved new venue','onlineUrl':'','version':e['version']})['request'];check('venue remains unchanged before amendment review',good(admin,'/events/'+aid)['eventInfo']['venue']=='Community venue');good(review,'/governance/requests/'+proposal['id']+'/decisions','POST',{'decision':'Approve','comment':'Venue reviewed','version':proposal['version']});check('approved amendment updates actual event',good(admin,'/events/'+aid)['eventInfo']['venue']=='Approved new venue');check('amendment notification fanout is queued',int(sql(f'SELECT count(*) FROM "EventNotices" WHERE "EventId"=\'{aid}\''))>=2)
# Document permissions, magic bytes, CSRF and existing secure storage.
pdf=b'%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF';code,doc=admin.upload('/events/'+aid+'/documents','agenda.pdf',pdf,{'title':'Program','kind':'Agenda'},'application/pdf');check('event document reuses secured storage',code==200);check('public participant may download event agenda',member.request('/events/'+aid+'/documents/'+doc['id'])[0]==200);check('mismatched upload rejected',admin.upload('/events/'+aid+'/documents','bad.png',pdf,{'title':'Bad','kind':'Gallery'},'image/png')[0]==400)
restricted=approve(create(title='Private document event '+tag,visibility='Restricted'));rid=restricted['id'];good(admin,'/events/'+rid+'/invitations','POST',{});code,doc=admin.upload('/events/'+rid+'/documents','agenda.pdf',pdf,{'title':'Private agenda','kind':'Agenda'},'application/pdf');check('anonymous restricted document denied',Client().request('/events/'+rid+'/documents/'+doc['id'])[0]==401)
check('event mutations require CSRF',admin.request('/events/'+aid+'/status','POST',b'{}',{'Content-Type':'application/json','X-CSRF':''})[0]==400)
# Conditional media delivery revalidates authorization and avoids repeat bytes.
code,body,headers=member.request('/events/'+aid+'/documents/'+good(admin,'/events/'+aid+'/documents')[0]['id']);etag=headers.get('ETag') or headers.get('Etag');check('event document returns private cache validator',code==200 and etag is not None);check('unchanged authorized document returns 304',member.request('/events/'+aid+'/documents/'+good(admin,'/events/'+aid+'/documents')[0]['id'],headers={'If-None-Match':etag})[0]==304)
# Five real attended guest responses exercise set-based summaries and CSV safety.
summary_event=approve(create(title='Survey summary '+tag,capacity=6));sid=summary_event['id'];good(admin,'/events/'+sid+'/feedback-form','PUT',{'questions':'[{"key":"rating","label":"Overall","type":"Rating","required":true}]','anonymous':True,'deadline':at(24)})
for i in range(5):good(admin,'/events/'+sid+'/guest-link','POST',{'name':'Survey Guest '+str(i),'email':f'survey-{tag}-{i}@example.test','consent':True})
deadline=time.monotonic()+15;links=[]
while time.monotonic()<deadline:
 links=[m['link'].split('token=')[1] for m in good(admin,'/development/mail') if m.get('email','').startswith('survey-'+tag+'-')]
 if len(links)==5:break
 time.sleep(.3)
assert len(links)==5
survey_guests=[]
for i,link in enumerate(links):
 c=Client();r=good(c,'/events/'+sid+'/registrations','POST',{'token':link,'category':'=SUM(1,2)' if i==0 else 'Guest'});good(admin,'/events/'+sid+'/checkins','POST',{'registrationId':r['id']});survey_guests.append((c,link,r))
se=good(admin,'/events/'+sid)['eventInfo'];good(admin,'/events/'+sid+'/status','POST',{'action':'Complete','version':se['version']})
for (c,link,r),rating in zip(survey_guests,[5,4,4,3,5]):good(c,'/events/'+sid+'/feedback/'+r['id'],'POST',{'token':link,'answers':json.dumps({'rating':rating})})
summary=good(admin,'/events/'+sid+'/feedback');check('five attended responses produce accurate SQL rating aggregate',summary['count']==5 and not summary['suppressed'] and abs(summary['ratings'][0]['average']-4.2)<.001);check('anonymous aggregate contains no individual response links',all('registrationId' not in str(x) for x in summary['ratings']));code,csv,h=admin.request('/events/'+sid+'/attendance/export');check('attendance CSV neutralizes guest-supplied category formulas',code==200 and b"'=SUM(1,2)" in csv)
# Consent preference suppresses real outbox notification.
sql(f'UPDATE "PersonPrivacy" SET "TransactionalEmail"=false WHERE "PersonId"=\'{person}\'');good(member,'/events/'+aid+'/rsvps','POST',{'response':'Going'});check('event notifications respect stored email consent',sql(f'SELECT "Suppressed" FROM "Mails" WHERE "Kind"=\'event.update\' AND "UserId"=(SELECT "UserId" FROM "Persons" WHERE "Id"=\'{person}\') ORDER BY "NextAttemptAt" DESC LIMIT 1')=='t');sql(f'UPDATE "PersonPrivacy" SET "TransactionalEmail"=true WHERE "PersonId"=\'{person}\'')
# Reminder is queued durably and processed by existing notification infrastructure.
sql(f'UPDATE "Events" SET "ReminderAt"=now()-interval \'1 second\',"ReminderSent"=false WHERE "Id"=\'{aid}\'');deadline=time.monotonic()+12
while time.monotonic()<deadline:
 if sql(f'SELECT "ReminderSent" FROM "Events" WHERE "Id"=\'{aid}\'')=='t':break
 time.sleep(.3)
check('scheduled reminder is durably claimed',sql(f'SELECT "ReminderSent" FROM "Events" WHERE "Id"=\'{aid}\'')=='t');check('reminder fanout uses bounded notice job',int(sql(f'SELECT count(*) FROM "EventNotices" WHERE "EventId"=\'{aid}\' AND "Message" LIKE \'Event reminder%\''))==1)
# Offset times normalize to UTC while the original zone is retained.
dst=create(title='DST boundary '+tag,startsAt='2027-03-14T01:30:00-05:00',endsAt='2027-03-14T03:30:00-04:00',registrationStartsAt='2027-03-01T00:00:00Z',registrationEndsAt='2027-03-14T06:00:00Z',timeZone='America/New_York');check('DST offset instants remain one hour apart',(datetime.datetime.fromisoformat(dst['endsAt'])-datetime.datetime.fromisoformat(dst['startsAt'])).total_seconds()==3600);check('invalid time-zone identifier rejected',admin.req('/events','POST',{**base,'timeZone':'Not/AZone'})[0]==400)
check('unknown registration form answer rejected',member.req('/events/'+rid+'/registrations','POST',{'responses':'{"unknown":"private"}'})[0]==400)
copy=good(admin,'/events/'+aid+'/duplicate','POST',{});check('duplication resets approval and ticket state',copy['approval']=='Draft' and copy['publication']=='Draft' and copy['approvalRequestId'] is None)
check('paid feature remains disabled server side',admin.req('/events','POST',{**base,'paid':True})[0]==409)
print(len(results),'extended Phase 4 checks passed')
