"""Real PostgreSQL finance workflow tests. Fictional records; no live payments."""
from support import Client,sql
import uuid,json,pathlib,datetime,concurrent.futures,subprocess
results=[];tag=uuid.uuid4().hex[:8];today=datetime.date.today();date=today.isoformat();start=f'{today.year}-01-01';end=f'{today.year}-12-31'
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/idcard-extended.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
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

import io,time,fitz,zipfile,hashlib
from PIL import Image,ImageDraw
import zxingcpp
base='/id-cards';f=json.loads(pathlib.Path('test-results/idcard-fixture.json').read_text());vid=f['versionId'];template=f['templateId'];active=sql(f"SELECT \"Id\" FROM \"DigitalCards\" WHERE \"MembershipId\"='{mid}' AND \"State\"='Issued' AND \"RevokedAt\" IS NULL")
# Backend policy and own-card privacy.
p=good(admin,base+'/policy');saved=good(admin,base+'/policy','PUT',{'allowMemberDownload':False,'allowClubDesign':True,'validityMonths':12,'defaultTemplateVersionId':vid,'version':p['version']});check('member download policy enforced',member.request('/my/id-card/download?id='+active)[0]==403);p=good(admin,base+'/policy','PUT',{'allowMemberDownload':True,'allowClubDesign':True,'validityMonths':12,'defaultTemplateVersionId':vid,'version':saved['version']});check('tenant default template stored',p['defaultTemplateVersionId']==vid)
check('invalid expiry denied',admin.req(base+'/issuances','POST',{'membershipId':mid,'templateVersionId':vid,'expiryAt':'2020-01-01T00:00:00Z'})[0]==400)
check('member cannot administer another photo',member.upload(base+'/members/'+mid+'/photo','bad.png',b'bad',content_type='image/png')[0]==403)
check('SVG upload rejected',admin.upload(base+'/assets','unsafe.svg',b'<svg><script>bad()</script></svg>',{'orgId':club},'image/svg+xml')[0]==400)
photo=Image.new('RGB',(256,320),'#b4c3a8');draw=ImageDraw.Draw(photo);draw.ellipse((60,40,196,176),fill='#426958');draw.rounded_rectangle((35,180,221,360),radius=40,fill='#426958');buffer=io.BytesIO();photo.save(buffer,format='PNG');photoBytes=buffer.getvalue()
code,asset=admin.upload(base+'/assets','licensed-fixture.png',photoBytes,{'orgId':club},'image/png');check('validated raster asset stored in existing vault',code==200 and sql(f"SELECT \"Folder\" FROM \"GovernanceDocuments\" WHERE \"Id\"='{asset['id']}'")=='ID Card Assets');check('asset version shares existing vault version table',sql(f"SELECT count(*) FROM \"DocumentVersions\" WHERE \"DocumentId\"='{asset['id']}'")=='1');check('asset library returns authorized image',any(x['id']==asset['id'] for x in good(admin,base+'/assets')))
code,uploaded=admin.upload(base+'/members/'+mid+'/photo','portrait.png',photoBytes,content_type='image/png');check('administrative photo upload uses original person record',code==200 and sql(f"SELECT octet_length(\"Photo\")>0 FROM \"Persons\" WHERE \"Id\"='{pid}'")=='t')
# Immutable inheritance, explicit bindings and required data validation.
design=good(admin,base+'/options')['design'];design['front']['elements'][2].update({'required':True,'radius':11,'width':22,'height':22});t=good(admin,base+'/templates','POST',{'orgId':club,'name':'Photo required '+tag,'design':design});v=good(admin,base+'/templates/'+t['id']+'/publish','POST',{})
othermid=sql(f"SELECT m.\"Id\" FROM \"Memberships\" m JOIN \"Persons\" p ON p.\"Id\"=m.\"PersonId\" WHERE m.\"ClubId\"='{club}' AND m.\"Status\"='Active' AND (m.\"EndsAt\" IS NULL OR m.\"EndsAt\">now()) AND p.\"Photo\" IS NULL AND p.\"Id\"<>'{pid}' LIMIT 1");assert othermid
check('required missing photograph blocks issuance',admin.req(base+'/issuances','POST',{'membershipId':othermid,'templateVersionId':v['id']})[0]==409)
child=json.loads(json.dumps(design));child['parentVersionId']=v['id'];inherited=good(admin,base+'/templates','POST',{'orgId':club,'name':'Inherited '+tag,'design':child});check('inherited template created',inherited['id']!=t['id']);bad=json.loads(json.dumps(child));bad['front']['elements'][1]['text']='Altered protected brand';check('inherited branding cannot change silently',admin.req(base+'/templates/'+inherited['id'],'PUT',{'orgId':club,'name':'Attempt','design':bad,'version':inherited['version']})[0]==409)
bindings=good(admin,base+'/members/'+mid+'/bindings');check('authorized live binding values resolved from CRM',bindings['values']['member.full_name']=='Maya Sen' and bindings['hasPhoto'] and 'birth_date' not in bindings['values'])
# Partial failures, failed-item retry and deterministic duplex output.
def approve(r):return good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent test issuance review','version':r['version']})
def wait_job(id):
 deadline=time.monotonic()+45
 while time.monotonic()<deadline:
  state=good(admin,base+'/bulk-jobs/'+id)
  if state['job']['status'] in ['Completed','Completed with errors','Cancelled','Failed']:return state
  time.sleep(.5)
 raise AssertionError('Job did not finish')
selection={'orgId':club,'membershipIds':[mid,othermid],'reissue':True};j=good(admin,base+'/bulk-jobs','POST',{'templateVersionId':v['id'],'selection':selection,'format':'Zip'});approve(j['request']);state=wait_job(j['job']['id']);check('partial failure preserves successful item',state['job']['status']=='Completed with errors' and state['job']['success']==1 and state['job']['failed']==1,state)
first=next(i for i in state['items'] if i['status']=='Succeeded');firstCard=first['cardId'];code,front,_=admin.request(base+'/issuances/'+firstCard+'/download?format=front');image=Image.open(io.BytesIO(front)).convert('RGB');bg=image.getpixel((round(1*300/25.4),round(20*300/25.4)));corner=image.getpixel((round(6*300/25.4),round(18*300/25.4)));center=image.getpixel((round(16*300/25.4),round(28*300/25.4)));check('actual PNG has circular photo clipping',corner==bg and center!=bg);pathlib.Path('test-results/idcard-photo-front.png').write_bytes(front)
code,_=admin.upload(base+'/members/'+othermid+'/photo','missing-fixed.png',photoBytes,content_type='image/png');assert code==200;good(admin,base+'/bulk-jobs/'+j['job']['id']+'/retry','POST',{});state=wait_job(j['job']['id']);check('retry processes only failed item',state['job']['success']==2 and state['job']['failed']==0 and next(i for i in state['items'] if i['membershipId']==mid)['cardId']==firstCard)
# A4 output uses the same physical positions tested independently with PDF text and decoded QRs.
printJob=good(admin,base+'/bulk-jobs','POST',{'templateVersionId':vid,'selection':selection,'format':'Print','print':{'sheetWidth':210,'sheetHeight':297,'margin':10,'gapX':4,'gapY':4,'sides':'Duplex','flip':'Long','offsetX':1,'offsetY':.5,'cropMarks':True}});approve(printJob['request']);printed=wait_job(printJob['job']['id']);check('print job completed',printed['job']['status']=='Completed');code,pdf,_=admin.request(base+'/bulk-jobs/'+printJob['job']['id']+'/download');assert code==200;pathlib.Path('test-results/idcard-print-a4.pdf').write_bytes(pdf);doc=fitz.open(stream=pdf,filetype='pdf');check('actual A4 duplex PDF has front and back pages',len(doc)==2 and all(abs(p.rect.width*25.4/72-210)<.02 and abs(p.rect.height*25.4/72-297)<.02 for p in doc));numbers={i['cardId']:sql(f"SELECT \"Number\" FROM \"Memberships\" WHERE \"Id\"='{i['membershipId']}'") for i in printed['items']};words=doc[0].get_text('words');check('both printed fronts survive later-card drawing',all(n in doc[0].get_text() for n in numbers.values()));check('first member ID matches 32mm design offset on sheet',any(abs(word[0]*25.4/72-(10+32))<.3 for word in words if word[4] in numbers.values()))
positions=good(admin,base+'/print-preview','POST',{'orgId':club,'name':'Independent positions','layout':{'sheetWidth':210,'sheetHeight':297,'margin':10,'gapX':4,'gapY':4,'sides':'Duplex','flip':'Long','offsetX':1,'offsetY':.5}})
for item in printed['items']:
 pos=positions['back'][item['ordinal']];roi=fitz.Rect(pos['x']*72/25.4,pos['y']*72/25.4,(pos['x']+85.6)*72/25.4,(pos['y']+53.98)*72/25.4);pix=doc[1].get_pixmap(matrix=fitz.Matrix(300/72,300/72),clip=roi);im=Image.open(io.BytesIO(pix.tobytes('png')));qr=zxingcpp.read_barcodes(im);check('duplex back QR maps to matching front ordinal '+str(item['ordinal']),len(qr)==1 and sql("SELECT \"Id\" FROM \"DigitalCards\" WHERE upper(\"TokenHash\")='"+hashlib.sha256(qr[0].text.rsplit('/',1)[1].encode()).hexdigest().upper()+"'")==item['cardId'])
# Save real PDF render artifacts for visual inspection.
for n,page in enumerate(doc):page.get_pixmap(matrix=fitz.Matrix(1.5,1.5)).save('test-results/idcard-print-page-'+str(n)+'.png')
# Official files remain immutable and every sensitive action is audited.
def guarded(q):
 try:sql(q);return False
 except subprocess.CalledProcessError:return True
issued=printed['items'][0]['cardId'];check('database blocks issued snapshot rewrite',guarded(f"UPDATE \"DigitalCards\" SET \"Snapshot\"='{{}}' WHERE \"Id\"='{issued}'"));check('database blocks published layout rewrite',guarded(f"UPDATE \"IdCardTemplateVersions\" SET \"Layout\"='{{}}' WHERE \"Id\"='{vid}'"));check('database blocks frozen selection rewrite',guarded(f"UPDATE \"IdCardJobItems\" SET \"MembershipId\"=gen_random_uuid() WHERE \"JobId\"='{printJob['job']['id']}'"))
check('download audit persisted',int(sql(f"SELECT count(*) FROM \"Audits\" WHERE \"Action\"='idcard.bulk.download' AND \"Resource\"='{printJob['job']['id']}'"))>=1)
check('member request recorded with attribution',good(member,base+'/issuances/'+issued+'/requests','POST',{'reason':'Fictional lost-card replacement request'})['message'].startswith('Request recorded')) if any(i['cardId']==issued and i['membershipId']==mid for i in printed['items']) else None
# Cancel a still-unapproved job, then reject approval without any side effects.
cancel=good(admin,base+'/bulk-jobs','POST',{'templateVersionId':vid,'selection':selection,'format':'Pairs'});good(admin,base+'/bulk-jobs/'+cancel['job']['id']+'/cancel','POST',{});check('cancelled job cannot be approved later',review.req('/governance/requests/'+cancel['request']['id']+'/decisions','POST',{'decision':'Approve','comment':'Too late','version':cancel['request']['version']})[0]==409);check('cancelled job produces no cards',good(admin,base+'/bulk-jobs/'+cancel['job']['id'])['job']['success']==0)
check('legacy reissue route cannot bypass designed-card approval',admin.req('/members/'+mid+'/cards/reissue','POST',{'version':int(sql(f"SELECT \"Version\" FROM \"Memberships\" WHERE \"Id\"='{mid}'"))})[0]==409)
print(len(results),'extended ID card checks passed')
