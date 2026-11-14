"""QA-only premium card issuance and member self-download integration."""
import os,json,pathlib,uuid,io
from support import Client
assert os.environ.get('CLUBS_TEST_URL')=='http://127.0.0.1:5095','Use the local QA service only.'
folder=pathlib.Path('docs/qa/id-card-upgrade');folder.mkdir(parents=True,exist_ok=True)
checks=[]
def good(c,p,m='GET',d=None):
 status,data=c.req(p,m,d);assert status in (200,204),(p,status,data);return data
def check(name,ok):
 checks.append({'test':name,'passed':bool(ok)});assert ok,name
admin=Client();good(admin,'/auth/login','POST',{'email':'saisatish@indianservers.com','password':__import__('support').PASSWORD})
review=Client();assert review.login('admin@example.test')[0]==200
member=Client();assert member.login('member.club1@example.test')[0]==200
other=Client();assert other.login('member.club2@example.test')[0]==200
cards=good(member,'/my/id-card');old=next((c for c in cards if c['state'] in ('Legacy','Issued') and not c['revokedAt']),None);assert old
check('Member metadata exposes membership number and name',bool(old['memberId'] and old['memberName']))
check('Member cannot design templates',member.req('/id-cards/templates')[0]==403)
options=good(admin,'/id-cards/options');design=options['design']
check('Premium default includes required name and membership ID',all(any(e['binding']==key and e['required'] for e in design['front']['elements']) for key in ['member.full_name','member.member_id']))
check('Premium card includes logo photo and secure QR',any(e['binding']=='association.logo' for e in design['front']['elements']) and any(e['kind']=='Qr' and e['width']>=20 for e in design['back']['elements']))
w=good(admin,'/workspaces')[0]
t=good(admin,'/id-cards/templates','POST',{'orgId':w['orgId'],'name':'Impact Signature Navy & Gold QA '+uuid.uuid4().hex[:6],'design':design})
v=good(admin,'/id-cards/templates/'+t['id']+'/publish','POST',{})
check('Premium template passes publication and QR constraints',bool(v['id']))
c=good(admin,'/id-cards/issuances','POST',{'membershipId':old['membershipId'],'templateVersionId':v['id'],'replacesId':old['id'],'reason':'QA premium member self-service card test'})
check('Unapproved draft cannot download',member.req('/my/id-card/download?id='+c['id'])[0]==409)
r=good(admin,'/id-cards/issuances/'+c['id']+'/submit','POST',{})['request']
check('Creator cannot approve own card',admin.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'QA self approval blocked','version':r['version']})[0] in (403,409))
good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent QA review of premium card design and membership','version':r['version']})
good(admin,'/id-cards/issuances/'+c['id']+'/generate','POST',{})
good(admin,'/id-cards/issuances/'+c['id']+'/issue','POST',{})
status,pdf,headers=member.request('/my/id-card/download-latest');check('Latest card downloads automatically as PDF',status==200 and pdf.startswith(b'%PDF'))
check('Download sends attachment filename','attachment' in headers.get('Content-Disposition','').lower())
(folder/'member-card.pdf').write_bytes(pdf)
for side in ['front','back']:
 status,png,_=member.request('/my/id-card/download?id='+c['id']+'&format='+side);check(side+' image download',status==200 and png.startswith(b'\x89PNG'));(folder/(side+'.png')).write_bytes(png)
check('Another member cannot download this card',other.req('/my/id-card/download?id='+c['id'])[0] in (403,404))
from PIL import Image
check('Front image is 300 DPI CR80 size',Image.open(folder/'front.png').size==(1012,638))
import pymupdf
pdfdoc=pymupdf.open(stream=pdf,filetype='pdf');check('PDF contains front and back pages',len(pdfdoc)==2)
check('PDF includes real member ID',old['memberId'] in ''.join(p.get_text() for p in pdfdoc))
check('PDF preserves CR80 physical dimensions',abs(pdfdoc[0].rect.width-85.6*72/25.4)<.1 and abs(pdfdoc[0].rect.height-53.98*72/25.4)<.1)
(folder/'checks.json').write_text(json.dumps({'checks':checks,'fixture':{'cardId':c['id'],'templateId':t['id'],'member':'member.club1@example.test'}},indent=2))
print(str(len(checks))+' ID card upgrade checks passed.')
