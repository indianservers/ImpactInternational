"""Real PostgreSQL ID-card workflow tests with fictional members."""
from support import Client,sql
import uuid,json,pathlib,datetime,concurrent.futures,subprocess
results=[];tag=uuid.uuid4().hex[:8];today=datetime.date.today();date=today.isoformat();start=f'{today.year}-01-01';end=f'{today.year}-12-31'
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('docs/idcard-pdf-acceptance.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code in (200,204),(p,code,b);return b
def login(email,role,org=None):
 c=Client();assert c.login(email)[0]==200;ws=good(c,'/workspaces');w=next(w for w in ws if w['role']==role and (not org or w['orgId']==org) and (role!='Association Super Admin' or w['association'].startswith('Global')));c.activate(w);return c,w
admin,w=login('admin@example.test','Association Super Admin');review,rw=login('secretary@example.test','Association Super Admin',w['orgId']);club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');member,mw=login('member@example.test','Club Member',club)
pid=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');mid=sql(f'SELECT "Id" FROM "Memberships" WHERE "PersonId"=\'{pid}\' AND "ClubId"=\'{club}\' AND "Status"=\'Active\' LIMIT 1');reviewid=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'secretary@example.test\'')



import time,io,pymupdf,zxingcpp
from PIL import Image
base='/id-cards';f=json.loads(pathlib.Path('test-results/idcard-fixture.json').read_text());current=sql(f"SELECT \"Id\" FROM \"DigitalCards\" WHERE \"MembershipId\"='{mid}' AND \"State\"='Issued' AND \"RevokedAt\" IS NULL");vid=sql(f"SELECT \"TemplateVersionId\" FROM \"DigitalCards\" WHERE \"Id\"='{current}'")
for name,width,height,flip,bleed in [('A3',297,420,'Long',True),('A3-landscape',420,297,'Short',True),('custom',180,130,'Long',False),('A4-short',210,297,'Short',False)]:
 preset={'sheetWidth':width,'sheetHeight':height,'margin':10,'gapX':4,'gapY':4,'sides':'Duplex','flip':flip,'offsetX':1,'offsetY':.5,'cropMarks':True,'includeBleed':bleed}
 slots=good(admin,base+'/print-preview','POST',{'orgId':club,'name':name,'layout':preset})
 j=good(admin,base+'/bulk-jobs','POST',{'templateVersionId':vid,'selection':{'orgId':club,'membershipIds':[mid]},'format':'Print','reuseIssued':True,'print':preset});r=j['request'];good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Review PDF geometry acceptance','version':r['version']})
 for _ in range(100):
  state=good(admin,base+'/bulk-jobs/'+j['job']['id'])['job']
  if state['status']=='Completed':break
  time.sleep(.1)
 assert state['status']=='Completed',state
 code,pdf,_=admin.request(base+'/bulk-jobs/'+state['id']+'/download');assert code==200
 doc=pymupdf.open(stream=pdf,filetype='pdf');check(name+' actual PDF page dimensions',len(doc)==2 and all(abs(p.rect.width*25.4/72-width)<.01 and abs(p.rect.height*25.4/72-height)<.01 for p in doc))
 check(name+' actual member text',any(midtext in doc[0].get_text() for midtext in ['Maya Sen','CS-000000003']))
 pos=slots['back'][0];b=3 if bleed else 0;x=(pos['x']+b+6)*72/25.4;y=(pos['y']+b+9)*72/25.4;clip=pymupdf.Rect(x,y,x+30*72/25.4,y+30*72/25.4);pix=doc[1].get_pixmap(matrix=pymupdf.Matrix(300/72,300/72),clip=clip);qr=zxingcpp.read_barcode(Image.open(io.BytesIO(pix.tobytes('png'))));check(name+' duplex QR decodes at calibrated physical placement',qr is not None)
 token=qr.text.split('/')[-1];snap=json.loads(sql(f"SELECT \"Snapshot\" FROM \"DigitalCards\" WHERE \"Id\"='{current}'"));check(name+' QR matches printed front identity',token==snap['values']['card.qr_code'].split('/')[-1])
 pathlib.Path('test-results/idcard-'+name+'.pdf').write_bytes(pdf)
print(len(results),'PDF acceptance checks passed')
