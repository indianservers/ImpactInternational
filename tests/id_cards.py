"""Real PostgreSQL ID-card workflow tests with fictional members."""
from support import Client,sql
import uuid,json,pathlib,datetime,concurrent.futures,subprocess
results=[];tag=uuid.uuid4().hex[:8];today=datetime.date.today();date=today.isoformat();start=f'{today.year}-01-01';end=f'{today.year}-12-31'
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/id-cards.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code in (200,204),(p,code,b);return b
def login(email,role,org=None):
 c=Client();assert c.login(email)[0]==200;ws=good(c,'/workspaces');w=next(w for w in ws if w['role']==role and (not org or w['orgId']==org) and (role!='Association Super Admin' or w['association'].startswith('Global')));c.activate(w);return c,w
admin,w=login('admin@example.test','Association Super Admin');review,rw=login('secretary@example.test','Association Super Admin',w['orgId']);club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');member,mw=login('member@example.test','Club Member',club)
pid=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');mid=sql(f'SELECT "Id" FROM "Memberships" WHERE "PersonId"=\'{pid}\' AND "ClubId"=\'{club}\' AND "Status"=\'Active\' LIMIT 1');reviewid=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'secretary@example.test\'')

from urllib.parse import quote
import urllib.request,time,fitz

import fitz,io,zipfile,math,time,urllib.request
from PIL import Image
import zxingcpp
base='/id-cards';options=good(admin,base+'/options');design=options['design']
check('typed placeholder registry',len(options['fields'])==18 and all(f['privacy'] and f['source'] and f['formats'] for f in options['fields']))
check('CR80 physical default',design['width']==85.6 and design['height']==53.98 and design['doubleSided'])
t=good(admin,base+'/templates','POST',{'orgId':club,'name':'QA membership '+tag,'design':design});check('template reuses certificate template master',sql(f"SELECT \"Type\" FROM \"CertificateTemplates\" WHERE \"Id\"='{t['id']}'")=='IdCard')
check('ordinary member cannot create template',member.req(base+'/templates','POST',{'orgId':club,'name':'Forbidden','design':design})[0]==403)
bad=json.loads(json.dumps(design));bad['front']['elements'][0]['binding']='member.birth_date';check('private unknown placeholder rejected',admin.req(base+'/templates','POST',{'orgId':club,'name':'Bad','design':bad})[0]==400)
bad=json.loads(json.dumps(design));bad['width']=0;check('invalid physical dimensions rejected',admin.req(base+'/templates','POST',{'orgId':club,'name':'Bad','design':bad})[0]==400)
t=good(admin,base+'/templates/'+t['id'],'PUT',{'orgId':club,'name':t['name']+' revised','design':design,'version':t['version']});check('template draft version advances',t['version']==2)
check('stale template edit rejected',admin.req(base+'/templates/'+t['id'],'PUT',{'orgId':club,'name':'Stale','design':design,'version':1})[0]==409)
v=good(admin,base+'/templates/'+t['id']+'/publish','POST',{});check('publishing creates separate immutable version',v['number']==1 and v['id']!=t['id'])
code,png,_=admin.request(base+'/templates/'+t['id']+'/preview','POST',json.dumps({'membershipId':mid}).encode(),{'Content-Type':'application/json'});check('server member preview renders actual PNG',code==200 and png.startswith(b'\x89PNG'),png[:200]);image=Image.open(io.BytesIO(png));check('300 DPI pixel dimensions',(image.width,image.height)==(math.ceil(85.6*300/25.4),math.ceil(53.98*300/25.4)))
pathlib.Path('test-results/idcard-preview.png').write_bytes(png)
old=sql(f"SELECT \"Id\" FROM \"DigitalCards\" WHERE \"MembershipId\"='{mid}' AND \"RevokedAt\" IS NULL AND \"State\" IN ('Legacy','Issued') LIMIT 1")
def draft(version=v['id'],replaces=None):return good(admin,base+'/issuances','POST',{'membershipId':mid,'templateVersionId':version,'replacesId':replaces,'reason':'Reviewed integration replacement'})
c=draft(replaces=old or None);check('issuance reuses digital card identity',sql(f"SELECT \"MembershipId\" FROM \"DigitalCards\" WHERE \"Id\"='{c['id']}'")==mid)
check('draft cannot generate before approval',admin.req(base+'/issuances/'+c['id']+'/generate','POST',{})[0]==409)
check('draft cannot be downloaded',member.request(base+'/issuances/'+c['id']+'/download')[0]==409)
r=good(admin,base+'/issuances/'+c['id']+'/submit','POST',{})['request'];check('creator cannot approve own card',admin.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Self','version':r['version']})[0]==403)
good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent card review','version':r['version']});check('shared review approves exact snapshot',good(admin,base+'/issuances/'+c['id'])['card']['state']=='Approved')
good(admin,base+'/issuances/'+c['id']+'/generate','POST',{});check('generated state precedes issuance',good(admin,base+'/issuances/'+c['id'])['card']['state']=='Generated')
if old:check('generation preserves previous card validity until issue',sql(f"SELECT \"RevokedAt\" IS NULL FROM \"DigitalCards\" WHERE \"Id\"='{old}'")=='t')
good(admin,base+'/issuances/'+c['id']+'/issue','POST',{});check('card formally issued',good(admin,base+'/issuances/'+c['id'])['card']['state']=='Issued')
if old:check('replacement revokes previous card transactionally',sql(f"SELECT \"State\" FROM \"DigitalCards\" WHERE \"Id\"='{old}'")=='Replaced')
code,pdf,_=member.request('/my/id-card/download?id='+c['id']);check('owner downloads real issued PDF',code==200 and pdf.startswith(b'%PDF'));pathlib.Path('test-results/idcard-issued.pdf').write_bytes(pdf);document=fitz.open(stream=pdf,filetype='pdf');check('two PDF sides',len(document)==2);check('PDF physical dimensions accurate',all(abs(p.rect.width*25.4/72-85.6)<.02 and abs(p.rect.height*25.4/72-53.98)<.02 for p in document));check('member data resolved in official PDF','Maya' in document[0].get_text())
code,front,_=member.request('/my/id-card/download?id='+c['id']+'&format=front');check('front PNG downloadable',code==200 and Image.open(io.BytesIO(front)).width==math.ceil(85.6*300/25.4))
code,back,_=member.request('/my/id-card/download?id='+c['id']+'&format=back');check('back PNG downloadable',code==200);pathlib.Path('test-results/idcard-back.png').write_bytes(back);decoded=zxingcpp.read_barcodes(Image.open(io.BytesIO(back)));check('QR contains only opaque verification URL',len(decoded)==1 and '/verify/id/' in decoded[0].text and 'Maya' not in decoded[0].text);token=decoded[0].text.rsplit('/',1)[1];public=Client();verified=good(public,'/public/id-cards/verify/'+token);check('QR verifies current membership',verified['valid'] and verified['serial']==c['serial']);check('verification does not expose sensitive fields',all(k not in json.dumps(verified).lower() for k in ['birthdate','contacts','address','phone','email']))
check('personal card listing includes official card',any(x['id']==c['id'] for x in good(member,'/my/id-card')))
check('another member cannot access private card',review.request('/my/id-card/download?id='+c['id'])[0]==404)
original=sql(f"SELECT \"Snapshot\" FROM \"DigitalCards\" WHERE \"Id\"='{c['id']}'");changed=json.loads(json.dumps(design));changed['front']['elements'][3]['text']='CHANGED';good(admin,base+'/templates/'+t['id'],'PUT',{'orgId':club,'name':'Changed draft','design':changed,'version':t['version']});v2=good(admin,base+'/templates/'+t['id']+'/publish','POST',{});check('published version history preserved',v2['number']==2 and len(good(admin,base+'/templates/'+t['id'])['versions'])==2);check('template edit does not mutate issued snapshot',sql(f"SELECT \"Snapshot\" FROM \"DigitalCards\" WHERE \"Id\"='{c['id']}'")==original);check('issued PDF bytes remain identical',member.request('/my/id-card/download?id='+c['id'])[1]==pdf)
print('core checks passed so far',len(results))
pathlib.Path('test-results/idcard-fixture.json').write_text(json.dumps({'templateId':t['id'],'versionId':v['id'],'version2Id':v2['id'],'cardId':c['id'],'token':token,'clubId':club,'membershipId':mid,'personId':pid,'tenantId':w['tenantId']},indent=2))

# Bulk processing executes outside the request and shares the governance engine.
selection={'orgId':club,'membershipIds':[mid],'reissue':True};summary=good(admin,base+'/bulk-preview','POST',selection);check('bulk preview counts current eligibility',summary['eligible']==1 and summary['alreadyIssued']==1)
bulk=good(admin,base+'/bulk-jobs','POST',{'templateVersionId':v['id'],'selection':selection,'format':'Zip'});job=bulk['job'];r=bulk['request'];check('bulk job starts pending review',job['status']=='Pending Approval' and job['total']==1)
check('bulk download denied before completion',admin.request(base+'/bulk-jobs/'+job['id']+'/download')[0]==409)
good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent bulk card review','version':r['version']})
deadline=time.monotonic()+40
while time.monotonic()<deadline:
 state=good(admin,base+'/bulk-jobs/'+job['id'])
 if state['job']['status'] in ['Completed','Completed with errors','Failed']:break
 time.sleep(.5)
check('durable worker generates and issues bulk card',state['job']['status']=='Completed' and state['job']['success']==1,state)
code,archive,_=admin.request(base+'/bulk-jobs/'+job['id']+'/download');check('bulk ZIP downloaded',code==200 and archive.startswith(b'PK'));z=zipfile.ZipFile(io.BytesIO(archive));check('ZIP contains PDF and both sides plus report',len(z.namelist())==4 and any(n.endswith('-front.png') for n in z.namelist()) and any(n.endswith('-back.png') for n in z.namelist()) and 'generation-report.csv' in z.namelist())
check('ordinary member cannot bulk export',member.request(base+'/bulk-jobs/'+job['id']+'/download')[0]==403)
check('replaced QR no longer verifies',not good(public,'/public/id-cards/verify/'+token)['valid'])
positions=good(admin,base+'/print-preview','POST',{'orgId':club,'name':'Long edge QA','layout':{'sheetWidth':210,'sheetHeight':297,'margin':10,'gapX':4,'gapY':4,'sides':'Duplex','flip':'Long'}});check('A4 physical sheet fits 8 CR80 slots',len(positions['front'])==8);check('long-edge duplex mirrors exact physical X coordinate',all(abs(f['x']+b['x']+85.6-210)<.001 and abs(f['y']-b['y'])<.001 for f,b in zip(positions['front'],positions['back'])))
short=good(admin,base+'/print-preview','POST',{'orgId':club,'name':'Short edge QA','layout':{'sheetWidth':210,'sheetHeight':297,'margin':10,'gapX':4,'gapY':4,'sides':'Duplex','flip':'Short'}});check('short-edge duplex mirrors exact physical Y coordinate',all(abs(f['y']+b['y']+53.98-297)<.001 and abs(f['x']-b['x'])<.001 for f,b in zip(short['front'],short['back'])))
check('invalid sheet rejects no-fit layout',admin.req(base+'/print-preview','POST',{'orgId':club,'name':'Too small','layout':{'sheetWidth':50,'sheetHeight':50}})[0]==400)
# Public QR does not authorize private downloads; current permission and tenant checks remain necessary.
issued=state['items'][0]['cardId'];check('anonymous download denied',public.request(base+'/issuances/'+issued+'/download')[0]==401)
other=next(x for x in good(admin,'/workspaces') if x['tenantId']!=w['tenantId']);admin.activate(other);check('cross tenant template inaccessible',admin.req(base+'/templates/'+t['id'])[0]==404);check('cross tenant private card inaccessible',admin.req(base+'/issuances/'+issued)[0]==404);check('cross tenant job inaccessible',admin.req(base+'/bulk-jobs/'+job['id'])[0]==404);admin.activate(w)
print(len(results),'ID card checks passed')
