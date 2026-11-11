"""Real PostgreSQL ID-card workflow tests with fictional members."""
from support import Client,sql
import uuid,json,pathlib,datetime,concurrent.futures,subprocess
results=[];tag=uuid.uuid4().hex[:8];today=datetime.date.today();date=today.isoformat();start=f'{today.year}-01-01';end=f'{today.year}-12-31'
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('docs/idcard-acceptance-edges.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code in (200,204),(p,code,b);return b
def login(email,role,org=None):
 c=Client();assert c.login(email)[0]==200;ws=good(c,'/workspaces');w=next(w for w in ws if w['role']==role and (not org or w['orgId']==org) and (role!='Association Super Admin' or w['association'].startswith('Global')));c.activate(w);return c,w
admin,w=login('admin@example.test','Association Super Admin');review,rw=login('secretary@example.test','Association Super Admin',w['orgId']);club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');member,mw=login('member@example.test','Club Member',club)
pid=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');mid=sql(f'SELECT "Id" FROM "Memberships" WHERE "PersonId"=\'{pid}\' AND "ClubId"=\'{club}\' AND "Status"=\'Active\' LIMIT 1');reviewid=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'secretary@example.test\'')


import time,copy
def export_existing():
 j=good(admin,'/id-cards/bulk-jobs','POST',{'templateVersionId':sql(f'''SELECT "TemplateVersionId" FROM "DigitalCards" WHERE "Id"='{current}' '''),'selection':{'orgId':club,'membershipIds':[mid]},'format':'Zip','reuseIssued':True});r=j['request'];good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Review existing card export','version':r['version']})
 for _ in range(100):
  state=good(admin,'/id-cards/bulk-jobs/'+j['job']['id'])['job']
  if state['status']=='Completed':return state['id']
  time.sleep(.1)
 raise AssertionError(state)

base='/id-cards';f=json.loads(pathlib.Path('test-results/idcard-fixture.json').read_text())
current=sql(f"SELECT \"Id\" FROM \"DigitalCards\" WHERE \"MembershipId\"='{mid}' AND \"State\"='Issued' AND \"RevokedAt\" IS NULL")
check('Owner lost-card request records attribution',good(member,base+'/issuances/'+current+'/requests','POST',{'reason':'Local acceptance lost-card request'})['message'].startswith('Request recorded'))
raw=sql(f"SELECT \"Snapshot\" FROM \"DigitalCards\" WHERE \"Id\"='{current}'");token=json.loads(raw)['values']['card.qr_code'].split('/')[-1]
verify='/public/id-cards/verify/'+token
check('Current QR valid before eligibility change',good(admin,verify)['valid'])
ends=sql(f"SELECT coalesce(\"EndsAt\"::text,'') FROM \"Memberships\" WHERE \"Id\"='{mid}'")
try:
 sql(f"UPDATE \"Memberships\" SET \"EndsAt\"=now()-interval '1 day' WHERE \"Id\"='{mid}'")
 check('Expired membership QR immediately invalid',not good(admin,verify)['valid'])
 check('Expired member excluded from bulk eligibility',good(admin,base+'/bulk-preview','POST',{'orgId':club,'membershipIds':[mid]})['eligible']==0)
finally:sql(f"UPDATE \"Memberships\" SET \"EndsAt\"="+("NULL" if not ends else "'"+ends+"'")+f" WHERE \"Id\"='{mid}'")
check('Restored eligibility revalidates unrevoked QR',good(admin,verify)['valid'])
draft=good(admin,base+'/issuances','POST',{'membershipId':mid,'templateVersionId':f['versionId']});req=good(admin,base+'/issuances/'+draft['id']+'/submit','POST',{})['request'];good(review,'/governance/requests/'+req['id']+'/decisions','POST',{'decision':'Approve','comment':'Review duplicate guard','version':req['version']});check('Duplicate official generation requires explicit replacement',admin.req(base+'/issuances/'+draft['id']+'/generate','POST',{})[0]==409)
before=sql(f"SELECT md5(row_to_json(c)::text) FROM \"DigitalCards\" c WHERE \"Id\"='{current}'")
job=export_existing();check('Existing-card export completes without changing identity or files',before==sql(f"SELECT md5(row_to_json(c)::text) FROM \"DigitalCards\" c WHERE \"Id\"='{current}'"))
expiredJob=export_existing();sql(f"UPDATE \"IdCardJobs\" SET \"ExpiresAt\"=now()-interval '1 day' WHERE \"Id\"='{expiredJob}'")
check('Expired export download rejected',admin.request(base+'/bulk-jobs/'+expiredJob+'/download')[0]==409)
revoked=good(admin,base+'/issuances/'+current+'/revoke','POST',{'reason':'Acceptance test deliberate revocation'})
check('Revocation immediately invalidates QR',not good(admin,verify)['valid'])
check('Revocation immediately blocks stale archive download',admin.request(base+'/bulk-jobs/'+job+'/download')[0]==409)
# Reviewed replacement restores a real current member card for mobile acceptance.
c=good(admin,base+'/issuances','POST',{'membershipId':mid,'templateVersionId':f['versionId']});req=good(admin,base+'/issuances/'+c['id']+'/submit','POST',{})['request'];good(review,'/governance/requests/'+req['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent edge-case review','version':req['version']});good(admin,base+'/issuances/'+c['id']+'/generate','POST',{});good(admin,base+'/issuances/'+c['id']+'/issue','POST',{})
check('Reviewed fresh issuance restores member download',member.request('/my/id-card/download?id='+c['id'])[0]==200)
versions=good(admin,base+'/templates');t=next(t for t in versions if t['id']==f['templateId']);check('Template library exposes historical published versions',len(t['versions'])>=2)
for key,value in [('country','NoSuchCountryAcceptance'),('memberStatus','Suspended'),('cardStatus','None'),('search','NoSuchMemberAcceptance')]:
 check('Bulk filter '+key+' narrows selection',good(admin,base+'/bulk-preview','POST',{'orgId':club,'membershipIds':[mid],key:value})['total']==0)

# A root-published locked logo is usable by a child designer without vault access.
from PIL import Image
import io
image=Image.new('RGB',(100,100),'#235f51');buf=io.BytesIO();image.save(buf,format='PNG')
code,asset=admin.upload(base+'/assets','inherited-brand.png',buf.getvalue(),{'orgId':w['orgId']},'image/png');assert code==200
options=good(admin,base+'/options');design=options['design'];logo=copy.deepcopy(design['front']['elements'][0]);logo.update(id=str(uuid.uuid4()),kind='Image',asset=asset['id'],binding='',x=70,y=3,width=8,height=8,locked=True);design['front']['elements'].append(logo)
parent=good(admin,base+'/templates','POST',{'orgId':w['orgId'],'name':'Parent artwork acceptance '+tag,'design':design});v=good(admin,base+'/templates/'+parent['id']+'/publish','POST',{})
manager,mgr=login('manager@example.test','Club Manager',club)
check('Child manager cannot download parent vault artwork directly',manager.request('/documents/'+asset['id']+'/download?preview=true')[0] in (403,404))
code,image,_=manager.request(base+'/versions/'+v['id']+'/assets/'+asset['id']);check('Published parent artwork preview accessible through scoped version',code==200 and image.startswith(b'\x89PNG'))
child=copy.deepcopy(design);child['parentVersionId']=v['id'];t=good(manager,base+'/templates','POST',{'orgId':club,'name':'Inherited artwork acceptance '+tag,'design':child});check('Child designer saves inherited parent artwork',t['id']!=parent['id'])
sibling=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-007\'');foreign=good(admin,base+'/templates','POST',{'orgId':sibling,'name':'Sibling restricted '+tag,'design':options['design']});check('Sibling template unavailable to club manager',manager.req(base+'/templates/'+foreign['id'])[0] in (403,404))
pathlib.Path('test-results/idcard-parent-ui.json').write_text(json.dumps({'templateId':t['id'],'assetId':asset['id'],'versionId':v['id']}))

# A reviewed request cannot generate after its template is archived.
arch=good(admin,base+'/templates','POST',{'orgId':club,'name':'Archive generation guard '+tag,'design':good(admin,base+'/options')['design']});av=good(admin,base+'/templates/'+arch['id']+'/publish','POST',{})
draft=good(admin,base+'/issuances','POST',{'membershipId':mid,'templateVersionId':av['id'],'replacesId':c['id'],'reason':'Reviewed archive guard fixture'});req=good(admin,base+'/issuances/'+draft['id']+'/submit','POST',{})['request'];good(review,'/governance/requests/'+req['id']+'/decisions','POST',{'decision':'Approve','comment':'Review archive guard','version':req['version']});good(admin,base+'/templates/'+arch['id']+'/archive','POST',{})
check('Archived template blocks already-approved generation',admin.req(base+'/issuances/'+draft['id']+'/generate','POST',{})[0]==409)
check('Archived template cannot publish another version',admin.req(base+'/templates/'+arch['id']+'/publish','POST',{})[0]==409)
policy=good(admin,base+'/policy');default=good(admin,base+'/issuances','POST',{'membershipId':mid,'templateVersionId':'00000000-0000-0000-0000-000000000000'});check('Empty version resolves tenant default template',default['templateVersionId']==policy['defaultTemplateVersionId'])
print(len(results),'acceptance edge checks passed')
