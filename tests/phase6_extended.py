"""Extended real-database checks, concurrency and independently decoded certificate QR."""
from support import Client,sql
import uuid,json,pathlib,time,datetime,concurrent.futures,io,zipfile
import pymupdf,zxingcpp
from PIL import Image
results=[];fixture=json.loads(pathlib.Path('test-results/phase6-fixture.json').read_text());tag=uuid.uuid4().hex[:8]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase6-extended.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,b=c.req(p,m,d);assert code==status or(status==200 and code==204),(p,code,b);return b
def login(email,role,org=None):
 c=Client();assert c.login(email)[0]==200;w=next(w for w in c.req('/workspaces')[1] if w['role']==role and(not org or w['orgId']==org));c.activate(w);return c,w
admin,w=login('admin@example.test','Association Super Admin',sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-001'"));review,_=login('secretary@example.test','Association Super Admin',w['orgId']);member,_=login('member@example.test','Club Member',fixture['clubId']);manager,_=login('manager@example.test','Club Manager',fixture['clubId']);now=datetime.datetime.now(datetime.timezone.utc);fmt=lambda d:d.isoformat()
def approve(r):
 r=good(admin,'/governance/requests/'+r['id'])['request'];
 if r['status']=='Approved':return r
 return good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','version':r['version'],'comment':'Independent extended review'})
# Decode the actual application-generated PDF, not a separate QR fixture.
pdf=pymupdf.open('test-results/phase6-certificate.pdf');pix=pdf[0].get_pixmap(matrix=pymupdf.Matrix(2,2));pathlib.Path('test-results/phase6-certificate.png').write_bytes(pix.tobytes('png'));barcodes=zxingcpp.read_barcodes(Image.open(io.BytesIO(pix.tobytes('png'))));check('actual rendered certificate QR independently decoded',len(barcodes)==1);token=barcodes[0].text.rsplit('/',1)[-1];public=Client();v=good(public,'/certificates/verify/'+token);check('public verification reflects revocation',not v['valid'] and v['status']=='Revoked');check('public privacy hides member identity and contacts',v['recipient'] is None and not any(k in v for k in ['email','phone','personId','sourceId','snapshot']));check('invalid verification token denied',public.req('/certificates/verify/not-a-token')[0]==404)
check('null input gets validation error',admin.req('/meetings','POST',{'orgId':fixture['clubId'],'title':None,'startsAt':fmt(now),'endsAt':fmt(now+datetime.timedelta(hours=1))})[0]==400)
# Reissue preserves historical revoked record and retries are idempotent.
body={'orgId':fixture['clubId'],'templateId':fixture['templateId'],'people':[fixture['personId']],'sourceType':'Project','sourceId':fixture['projectId'],'replacesId':fixture['certificateId'],'publicName':True}
prepared=good(admin,'/certificates','POST',body)[0];retry=good(admin,'/certificates','POST',body)[0];check('certificate retry returns same issuance',prepared['id']==retry['id']);approve(good(admin,'/governance/requests/'+prepared['requestId'])['request']);code,b,h=admin.request('/certificates/'+prepared['id']+'/pdf');check('reissued certificate generates PDF',code==200 and b.startswith(b'%PDF'));render=pymupdf.open(stream=b,filetype='pdf')[0].get_pixmap(matrix=pymupdf.Matrix(2,2));decoded=zxingcpp.read_barcodes(Image.open(io.BytesIO(render.tobytes('png'))));token2=decoded[0].text.rsplit('/',1)[-1];v=good(public,'/certificates/verify/'+token2);check('issued public verification has approved minimal name',v['valid'] and bool(v['recipient']));check('historical revoked certificate stays revoked',not good(public,'/certificates/verify/'+token)['valid'])
code,b,h=admin.request('/certificates/bulk-pdf','POST',json.dumps([prepared['id']]).encode(),{'Content-Type':'application/json'});check('bulk actual PDF ZIP generated',code==200 and zipfile.ZipFile(io.BytesIO(b)).read(zipfile.ZipFile(io.BytesIO(b)).namelist()[0]).startswith(b'%PDF'))
# Source eligibility and template safety.
t=good(admin,'/certificate-templates','POST',{'orgId':fixture['clubId'],'name':'Missing context '+tag,'type':'Service','layout':{'elements':[{'text':'{{unknown_private_field}}','x':50,'y':50,'size':20}]}}) if False else None
layout={'orientation':'Landscape','background':'#fffdf5','border':'#b39755','elements':[{'text':'{{missing_value}}','x':50,'y':50,'size':20}]}
t=good(admin,'/certificate-templates','POST',{'orgId':fixture['clubId'],'name':'Missing context '+tag,'type':'Service','layout':layout});check('missing issuance placeholder rejected',admin.req('/certificates','POST',{**body,'templateId':t['id'],'replacesId':None})[0]==400)
check('hours source cannot certify unverified volunteer',admin.req('/certificates','POST',{**body,'people':[fixture['managerPersonId']],'replacesId':None})[0]==400)
# Capacity cannot be exceeded by concurrent attempts to approve pending applications.
good(manager,'/volunteers','POST',{'personId':fixture['managerPersonId'],'training':'Child safeguarding'});waiting=good(manager,'/volunteers/shifts/'+fixture['shiftId']+'/apply','POST',{});check('full shift creates waiting-list application',waiting['status']=='Waiting');check('coordinator cannot overbook full shift',admin.req('/volunteers/assignments/'+waiting['id']+'/status','POST',{'status':'Approved','version':waiting['version']})[0]==409)
shift=good(admin,'/volunteers/shifts','POST',{'projectId':fixture['projectId'],'title':'Capacity race '+tag,'startsAt':fmt(now+datetime.timedelta(days=1)),'endsAt':fmt(now+datetime.timedelta(days=1,hours=2)),'deadline':fmt(now+datetime.timedelta(hours=8)),'supervisorId':fixture['managerPersonId'],'capacity':1});aa=good(member,'/volunteers/shifts/'+shift['id']+'/apply','POST',{});bb=good(manager,'/volunteers/shifts/'+shift['id']+'/apply','POST',{})
with concurrent.futures.ThreadPoolExecutor(2) as pool:
 codes=list(pool.map(lambda a:admin.req('/volunteers/assignments/'+a['id']+'/status','POST',{'status':'Approved','version':a['version']})[0],[aa,bb]))
check('concurrent approvals respect capacity transaction',sorted(codes)==[200,409],codes)
# Actual check-in/check-out, using an explicit database test-clock adjustment.
assignment=aa if codes[0]==200 else bb;person=assignment['personId'];assignment=next(a for a in good(admin,'/volunteers/assignments?shiftId='+shift['id']) if a['id']==assignment['id']);a=good(admin,'/volunteers/assignments/'+assignment['id']+'/attendance','POST',{'action':'CheckIn'});check('actual check-in timestamp persisted',a['checkIn'] is not None);sql(f"UPDATE \"VolunteerAssignments\" SET \"CheckIn\"=now()-interval '2 hour' WHERE \"Id\"='{a['id']}'");a=good(admin,'/volunteers/assignments/'+a['id']+'/attendance','POST',{'action':'CheckOut','breakMinutes':15});h=next(h for h in good(admin,'/volunteers/hours?projectId='+fixture['projectId']) if h['assignmentId']==a['id']);check('attendance duration minus actual break used',abs(h['hours']-1.75)<.02);check('scheduled hours are not auto verified',h['status']=='Submitted')
# Event linkage projects actual existing event check-in without duplicate meeting attendance.
event=sql(f"SELECT e.\"Id\" FROM \"Events\" e WHERE e.\"OrgId\"='{fixture['clubId']}' AND EXISTS(SELECT 1 FROM \"EventCheckins\" c WHERE c.\"EventId\"=e.\"Id\" AND NOT c.\"Voided\" AND c.\"SessionId\" IS NULL) LIMIT 1")
assert event,'Run Phase 4 fixtures before this suite'
m=good(admin,'/meetings','POST',{'orgId':fixture['clubId'],'title':'Linked attendance '+tag,'startsAt':fmt(now),'endsAt':fmt(now+datetime.timedelta(hours=1)),'eventId':event});att=good(admin,'/meetings/'+m['id']+'/attendance');check('linked meeting reads event check-ins',att['source']=='Event' and att['present']>0);check('linked meeting refuses duplicate attendance write',admin.req('/meetings/'+m['id']+'/attendance','POST',{'personId':fixture['personId'],'status':'Present'})[0]==409);check('linked meeting stores no duplicate attendance',sql(f"SELECT count(*) FROM \"MeetingAttendances\" WHERE \"MeetingId\"='{m['id']}'")=='0')
# Configured voting categories explicitly exclude non-voters despite physical attendance.
othercat=sql(f"SELECT \"Id\" FROM \"MembershipCategories\" WHERE \"TenantId\"='{w['tenantId']}' AND \"Id\" NOT IN(SELECT \"CategoryId\" FROM \"Memberships\" WHERE \"PersonId\"='{fixture['personId']}' AND \"ClubId\"='{fixture['clubId']}') LIMIT 1")
if othercat:
 m2=good(admin,'/meetings','POST',{'orgId':fixture['clubId'],'title':'Voting eligibility '+tag,'startsAt':fmt(now),'endsAt':fmt(now+datetime.timedelta(hours=1)),'votingCategories':[othercat]});good(admin,'/meetings/'+m2['id']+'/attendance','POST',{'personId':fixture['personId'],'status':'Present'});q=good(admin,'/meetings/'+m2['id']+'/attendance');check('physical attendance does not grant voting eligibility',q['present']==1 and q['votingPresent']==0)
# Sharing is explicit, never implied by senior hierarchy.
doc=fixture['documentId'];good(admin,'/documents/'+doc,'PUT',{'title':'Explicitly shared QA record','folder':'Board','tags':'qa','visibility':'Private','sharedUsers':[sql("SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='secretary@example.test'")]});check('explicit document sharing grants chosen reviewer',review.request('/documents/'+doc+'/download')[0]==200)
r=good(admin,'/documents/'+doc+'/submit','POST',{});approve(r);check('shared document approved with version binding',good(admin,'/documents/'+doc)['document']['status']=='Available')
# Generic reminders use the shared outbox, and one transaction marks the delivery cycle.
task=good(admin,'/tasks','POST',{'orgId':fixture['clubId'],'title':'Reminder '+tag,'personId':fixture['personId'],'dueAt':fmt(now-datetime.timedelta(hours=1))});before=int(sql("SELECT count(*) FROM \"Mails\" WHERE \"Kind\"='operations.update' AND \"Channel\"='InApp'"));deadline=time.monotonic()+30
while time.monotonic()<deadline:
 if sql(f"SELECT \"RemindedAt\" IS NOT NULL FROM \"OperationTasks\" WHERE \"Id\"='{task['id']}'")=='t':break
 time.sleep(.5)
check('durable reminder marks cycle',sql(f"SELECT \"RemindedAt\" IS NOT NULL FROM \"OperationTasks\" WHERE \"Id\"='{task['id']}'")=='t');after=int(sql("SELECT count(*) FROM \"Mails\" WHERE \"Kind\"='operations.update' AND \"Channel\"='InApp'"));check('reminder uses existing in-app communication outbox',after>before)
# Retention removes bytes and keeps metadata/hash evidence.
code,d=admin.upload('/documents','expiry.pdf',b'%PDF-1.4\nFictional expired document\n%%EOF',{'orgId':fixture['clubId'],'title':'Expiry '+tag,'visibility':'Private'},'application/pdf');assert code==200
key=sql(f"SELECT \"ObjectKey\" FROM \"GovernanceDocuments\" WHERE \"Id\"='{d['id']}'");sql(f"UPDATE \"GovernanceDocuments\" SET \"RetainUntil\"=now()-interval '1 minute' WHERE \"Id\"='{d['id']}'");check('expired object immediately inaccessible',admin.request('/documents/'+d['id']+'/download')[0]==410);deadline=time.monotonic()+30
while time.monotonic()<deadline:
 if sql(f"SELECT \"Status\" FROM \"GovernanceDocuments\" WHERE \"Id\"='{d['id']}'")=='Expired':break
 time.sleep(.5)
check('retention deletes private bytes and retains metadata',not pathlib.Path('.infra/communication-objects',key).exists() and sql(f"SELECT \"Status\" FROM \"GovernanceDocuments\" WHERE \"Id\"='{d['id']}'")=='Expired')
print(str(len(results))+' extended Phase 6 checks passed')
