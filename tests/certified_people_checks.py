"""Certification portal checks; requires an isolated migrated QA database and API."""
import json,os,pathlib,uuid
from support import Client,sql,PASSWORD
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_certified_people_qa_')
checks=[];out=pathlib.Path('docs/qa/certified-people');out.mkdir(parents=True,exist_ok=True)
def call(c,path,method='GET',body=None,status=200):
 code,data=c.req(path,method,body);assert code==status,(path,code,data);return data
def check(name,ok=True):
 checks.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,name
def login(email):
 c=Client();call(c,'/auth/login','POST',{'email':email,'password':PASSWORD});return c
admin=login('admin@example.test');member=login('member.club1@example.test');clubpres=login('club.president@example.test');anon=Client()
call(anon,'/certifications',status=401);call(anon,'/portal/enroll','POST',{},401);check('Anonymous certificate access and enrollment denied')
call(member,'/platform/participants',status=403);call(clubpres,'/platform/participants',status=403);check('Register restricted to Platform Admin')
email='learner-'+uuid.uuid4().hex[:8]+'@example.test';name='Independent Workshop Graduate'
call(anon,'/auth/register','POST',{'name':name,'email':email,'phone':'+919999999999','password':PASSWORD,'confirmation':PASSWORD,'agreed':True},202)
# Complete the real verification endpoint using the email token queued in the isolated test database.
link=sql('SELECT "Link" FROM "Mails" m JOIN "Users" u ON u."Id"=m."UserId" WHERE u."Email"=\''+email+'\' ORDER BY m."Id" DESC LIMIT 1')
from urllib.parse import urlparse,parse_qs
token=parse_qs(urlparse(link).query)['token'][0]
call(anon,'/auth/complete','POST',{'token':token,'password':''});learner=login(email);check('Independent participant registers and verifies email without club selection')
check('Independent account starts without workspaces',call(learner,'/workspaces')==[])
check('Certification portal is available without workspace',call(learner,'/portal/me')['enrolled']==False)
programs=call(learner,'/certifications');ttt=next(p for p in programs if p['name']=='Train the Trainer');check('Train the Trainer program available to standalone accounts')
identity=call(learner,'/portal/enroll','POST',{});call(learner,'/portal/enroll','POST',{});pid=identity['id'];check('Enrollment idempotent and grants no role',call(learner,'/workspaces')==[] and sql('SELECT count(*) FROM "PortalParticipants" WHERE "PersonId"=\''+pid+'\'')=='1')
certbody={'programId':ttt['id'],'certificateId':'TTT-'+uuid.uuid4().hex[:10],'batch':'TTT November workshop','issuer':'Impact International','issuedOn':'2026-11-01','validUntil':'2027-11-01'}
cert=call(learner,'/member-certifications','POST',certbody);check('Independent participant records workshop certification',cert['personId']==pid)
call(learner,'/member-certifications','POST',certbody,409);check('Duplicate certificate numbers rejected')
call(member,'/member-certifications?personId='+pid,status=403);check('Ordinary member cannot read another participant certifications')
reg=call(admin,'/platform/participants?search='+email);row=next(p for p in reg['items'] if p['id']==pid);check('Independent graduate visible in admin register with no club',row['clubs']==[] and len(row['certificates'])==1)
call(learner,'/platform/participants/certifications/'+cert['id']+'/verify','POST',{'version':cert['version']},403)
call(admin,'/platform/participants/certifications/'+cert['id']+'/verify','POST',{'version':cert['version']-1},409)
call(admin,'/platform/participants/certifications/'+cert['id']+'/verify','POST',{'version':cert['version']},204)
current=call(learner,'/member-certifications')[0];check('Only Platform Admin verifies current certificate version',current['verification']=='Platform verified')
updated={**certbody,'batch':'Corrected workshop batch','version':current['certification']['version']};call(learner,'/member-certifications/'+cert['id'],'PUT',updated);check('Editing certificate clears verification',call(learner,'/member-certifications')[0]['verification']=='Self reported')
# Club members use their existing global identity and are listed once.
before=call(member,'/portal/me')['person']['id'];enrolled=call(member,'/portal/enroll','POST',{});check('Club member retains existing person profile',enrolled['id']==before)
clubcert=call(member,'/member-certifications','POST',{**certbody,'certificateId':'TTT-CLUB-'+uuid.uuid4().hex[:8]})
check('Club member is also a training graduate',clubcert['personId']==before and any(x['id']==before for x in call(admin,'/platform/participants?kind=Club&programId='+ttt['id'])['items']))
added=call(admin,'/platform/participants','POST',{'name':'Existing club graduate','email':'member.club1@example.test'});check('Admin adding existing member reuses profile',added['id']==before)
# Admin can record alumni before account registration; verified email later claims it.
alumni_email='alumni-'+uuid.uuid4().hex[:8]+'@example.test';alumni=call(admin,'/platform/participants','POST',{'name':'Alumni Awaiting Account','email':alumni_email})
alumni_cert=call(admin,'/member-certifications?personId='+alumni['id'],'POST',{**certbody,'certificateId':'ALUMNI-'+uuid.uuid4().hex[:8]});check('Admin records standalone alumni with certifications before signup',alumni_cert['personId']==alumni['id'])
call(anon,'/auth/register','POST',{'name':'Alumni Awaiting Account','email':alumni_email,'phone':'+919999999998','password':PASSWORD,'confirmation':PASSWORD,'agreed':True},202)
link=sql('SELECT "Link" FROM "Mails" m JOIN "Users" u ON u."Id"=m."UserId" WHERE u."Email"=\''+alumni_email+'\' ORDER BY m."Id" DESC LIMIT 1');token=parse_qs(urlparse(link).query)['token'][0];call(anon,'/auth/complete','POST',{'token':token,'password':''})
alumni_user=login(alumni_email);claimed=call(alumni_user,'/portal/enroll','POST',{});check('Verified email claims admin-created alumni profile without duplicates',claimed['id']==alumni['id'] and len(call(alumni_user,'/member-certifications'))==1)
check('Independent filter excludes active club members',all(not p['clubs'] for p in call(admin,'/platform/participants?kind=Independent')['items']))
(out/'api-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8');(out/'fixture.json').write_text(json.dumps({'email':email,'personId':pid,'memberPersonId':before,'programId':ttt['id'],'alumniEmail':alumni_email},indent=2),encoding='utf-8')
print(str(len(checks))+' checks passed')
