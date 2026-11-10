"""Phase 2 behavior, privacy and concurrency checks against actual PostgreSQL and HTTP."""
from support import Client,PASSWORD,sql,BASE
import json,uuid,time,pathlib,concurrent.futures,io,csv,base64,urllib.parse,urllib.request,re,zipfile
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name, '' if ok else str(data)[:700]);pathlib.Path('test-results').mkdir(exist_ok=True);pathlib.Path('test-results/phase2.json').write_text(json.dumps({'tests':results},indent=2));assert ok,(name,data)
def good(client,path,method='GET',body=None,status=200):
 code,data=client.req(path,method,body);check(method+' '+path.split('?')[0],code==status,data);return data
def signup(name):
 c=Client();email='phase2-'+uuid.uuid4().hex+'@example.test';check('new applicant signup',c.req('/auth/register','POST',{'name':name,'email':email,'phone':'+910000000000','password':PASSWORD,'confirmation':PASSWORD,'agreed':True})[0]==202)
 check('unverified applicant blocked',c.login(email)[0]==401);mail=next(x for x in c.req('/development/mail')[1] if x['email']==email and x['kind']=='verify');token=mail['link'].split('token=')[1].split('&')[0];check('applicant email verification',c.req('/auth/complete','POST',{'token':token,'password':''})[0]==200);check('applicant sign in',c.login(email)[0]==200);return c,email
admin=Client();check('Phase 2 administrator login',admin.login('admin@example.test')[0]==200);workspaces=admin.req('/workspaces')[1];root=next(w for w in workspaces if w['role']=='Association Super Admin' and w['association'].startswith('Global'));other=next(w for w in workspaces if w['association']=='Civic Futures Network');admin.activate(root)
config=good(admin,'/membership/config');regular=next(c for c in config['categories'] if c['code']=='REG');honorary=next(c for c in config['categories'] if c['code']=='HON');clubs=good(admin,'/organizations?clubs=true&status=Active&size=100')['items'];source=next(c for c in clubs if c['code']=='GC-006');destination=next(c for c in clubs if c['code']=='GC-008')
secretary=Client();secretary.login('secretary@example.test');sw=next(w for w in secretary.req('/workspaces')[1] if w['role']=='Club Secretary');secretary.activate(sw)
president=Client();president.login('president@example.test');pw=next(w for w in president.req('/workspaces')[1] if w['role']=='Club President');president.activate(pw)
reader=Client();reader.login('member@example.test');rw=next(w for w in reader.req('/workspaces')[1] if w['role']=='Club Member' and w['orgId']==source['id']);reader.activate(rw)
applicant,email=signup('Integration Applicant '+uuid.uuid4().hex[:8]);check('member portal works without administrative workspace',applicant.req('/membership/me')[0]==200)
invite=good(admin,'/membership/invitations','POST',{'clubId':source['id'],'categoryId':regular['id'],'email':email,'expiresInDays':7,'usageLimit':1});token=invite['link'].split('token=')[1]
accepted=good(applicant,'/membership/invitations/accept','POST',{'token':token});app_id=accepted['id'];check('invitation only creates a draft',accepted['kind']=='Application draft' and not applicant.req('/membership/me')[1]['memberships'])
check('duplicate invitation processing denied',applicant.req('/membership/invitations/accept','POST',{'token':token})[0]==409)
expired=good(admin,'/membership/invitations','POST',{'clubId':source['id'],'categoryId':regular['id'],'expiresInDays':1,'usageLimit':1});sql(f'UPDATE "MembershipInvitations" SET "CreatedAt"=now()-interval \'2 days\',"ExpiresAt"=now()-interval \'1 day\' WHERE "Id"=\'{expired["id"]}\'');check('expired invitation denied',applicant.req('/membership/invitations/accept','POST',{'token':expired['link'].split('token=')[1]})[0]==409)
wrong=good(admin,'/membership/invitations','POST',{'clubId':source['id'],'categoryId':regular['id'],'email':'someone-else@example.test'});check('invitation intended recipient enforced',applicant.req('/membership/invitations/accept','POST',{'token':wrong['link'].split('token=')[1]})[0]==403)
profile=good(applicant,'/membership/me')['profile'];name=profile['legalName'];person_id=profile['id'];shared_phone='+910001112222';person={'legalName':name,'displayName':name,'birthDate':'1990-04-12','profession':'Physician','industry':'Healthcare','contacts':[{'kind':'Email','label':'Personal','value':email},{'kind':'Phone','label':'Family','value':shared_phone},{'kind':'Emergency','label':'Emergency family','value':'+910009998888'}],'addresses':[{'country':'India','state':'Andhra Pradesh','city':'Vijayawada','postalCode':'000000','address':'Restricted test address'}],'skills':['Leadership'],'expertise':['Health'],'languages':['English'],'version':profile['version']}
check('member profile update',applicant.req('/persons/me','PUT',person)[0]==200)
invalid={**person,'birthDate':'2099-01-01','version':2};check('field-level date validation',applicant.req('/persons/me','PUT',invalid)[0]==400)
a=good(applicant,'/membership/applications/'+app_id)['application'];a=good(applicant,'/membership/applications/'+app_id,'PUT',{'clubId':source['id'],'categoryId':regular['id'],'statement':'I would like to serve our community.','version':a['version']});check('draft is not a membership',a['status']=='Draft' and not applicant.req('/membership/me')[1]['memberships'])
a=good(applicant,'/membership/applications/'+app_id+'/submit','POST',{'version':a['version']});check('submitted application has no membership yet',a['status']=='Submitted' and not applicant.req('/membership/me')[1]['memberships'])
check('president cannot skip secretary stage',president.req('/membership/applications/'+app_id+'/decisions','POST',{'decision':'Approve','comment':'','version':a['version']})[0]==403)
a=good(secretary,'/membership/applications/'+app_id+'/decisions','POST',{'decision':'RequestChanges','comment':'Please clarify the service statement.','version':a['version']});check('request changes state',a['status']=='Changes Requested')
a=good(applicant,'/membership/applications/'+app_id,'PUT',{'clubId':source['id'],'categoryId':regular['id'],'statement':'Updated service statement with more detail.','version':a['version']});a=good(applicant,'/membership/applications/'+app_id+'/submit','POST',{'version':a['version']})
second_secretary=Client();second_secretary.login('secretary@example.test');second_secretary.activate(sw)
def approve(c):return c.req('/membership/applications/'+app_id+'/decisions','POST',{'decision':'Approve','comment':'Secretary review complete.','version':a['version']})[0]
with concurrent.futures.ThreadPoolExecutor(2) as pool:codes=list(pool.map(approve,(secretary,second_secretary)))
check('concurrent approval attempts have one winner',sorted(codes)==[200,409],codes)
a=good(applicant,'/membership/applications/'+app_id)['application'];check('secretary approval advances to president',a['currentSequence']==2 and a['status']=='Under Review')
a=good(president,'/membership/applications/'+app_id+'/decisions','POST',{'decision':'Approve','comment':'President approval recorded.','version':a['version']});check('president approval activates membership',a['status']=='Approved')
portal=good(applicant,'/membership/me');member=portal['memberships'][0];membership_id=member['id'];check('one activation for approved application',len(portal['memberships'])==1 and member['status']=='Active' and member['personId']==person_id)
aw=next(w for w in applicant.req('/workspaces')[1] if w['orgId']==source['id']);applicant.activate(aw)
check('ordinary member cannot access administration CRM',applicant.req('/members')[0]==403)
check('directory hides opted-out person',not any(x['personId']==person_id for x in reader.req('/directory?search='+urllib.parse.quote(name))[1]['items']))
check('unauthorized private profile denied to directory role',reader.req('/members/'+membership_id)[0]==403)
check('own full profile is accessible',applicant.req('/members/'+membership_id)[0]==200)
private=good(secretary,'/members/'+membership_id);check('sensitive fields removed before serialization',private['person']['birthDate'] is None and not any(c['kind']=='Emergency' for c in private['person']['contacts']))
privacy=portal['profile']['privacy'];privacy.update(directoryVisible=True,displayEmail=False,displayPhone=False,professionalVisible=False,cardNameVisible=False)
privacy=good(applicant,'/persons/me/privacy','PUT',privacy)
listing=good(reader,'/directory?search='+urllib.parse.quote(name));visible=next(x for x in listing['items'] if x['personId']==person_id);check('directory field-level privacy',visible['email'] is None and visible['phone'] is None and visible['profession'] is None)
dir_profile=good(reader,'/members/'+membership_id+'?directory=true');check('directory profile never includes birth date or emergency data',dir_profile['person']['birthDate'] is None and dir_profile['person']['legalName'] is None and not dir_profile['person']['contacts'])
privacy.update(displayEmail=True,professionalVisible=True);privacy=good(applicant,'/persons/me/privacy','PUT',privacy);visible=next(x for x in good(reader,'/directory?profession=Physician&search='+urllib.parse.quote(name))['items'] if x['personId']==person_id);check('professional directory is opt-in',visible['profession']=='Physician' and visible['email']==email and visible['phone'] is None)
check('unauthorized export denied',reader.req('/membership/export','POST',{'format':'csv'})[0]==403)
# Actual downloadable card contains a PNG QR code; public verification returns only its approved field set.
status,svg,h=applicant.request('/members/'+membership_id+'/card');check('server-rendered downloadable digital card',status==200 and b'<svg' in svg and b'data:image/png;base64,' in svg)
cards=good(applicant,'/members/'+membership_id+'/cards');card=next(x for x in cards if x['revokedAt'] is None)
# Decode the QR produced by the server when OpenCV is installed; otherwise use an authenticated verification path.
qr_url=None
try:
 import zxingcpp
 from PIL import Image
 png=base64.b64decode(re.search(rb"data:image/png;base64,([^\x27]+)",svg).group(1));decoded=zxingcpp.read_barcode(Image.open(io.BytesIO(png)));qr_url=decoded.text if decoded else None
except ImportError:pass
try:
 import cv2,numpy as np
 png=base64.b64decode(re.search(rb'data:image/png;base64,([^\x27]+)',svg).group(1));qr_url=cv2.QRCodeDetector().detectAndDecode(cv2.imdecode(np.frombuffer(png,dtype=np.uint8),cv2.IMREAD_GRAYSCALE))[0]
except ImportError:pass
if not qr_url:qr_url=card.get('verificationPath')
check('QR uses an opaque identifier rather than personal details',bool(qr_url) and 'token=' in qr_url and email not in qr_url and '1990' not in qr_url,qr_url)
card_token=urllib.parse.parse_qs(urllib.parse.urlparse(qr_url).query)['token'][0];verify=good(Client(),'/cards/verify/'+card_token);check('public verification minimizes PII',verify['valid'] and verify['name']=='Verified member' and set(verify)<=set(['valid','name','club','status','expiresAt','message']))
good(applicant,'/membership/cards/'+card['id']+'/revoke','POST',{},204);check('revoked QR does not verify',Client().req('/cards/verify/'+card_token)[1]['valid'] is False);good(applicant,'/members/'+membership_id+'/cards/reissue','POST',{'version':member['version']});check('reissuance does not revive old QR',Client().req('/cards/verify/'+card_token)[1]['valid'] is False)
# Shared contact details are reviewed, never merged automatically.
manual={'person':{'legalName':'Family Person '+uuid.uuid4().hex[:8],'displayName':'Family Person','contacts':[{'kind':'Phone','label':'Family','value':shared_phone}],'addresses':[]},'clubId':source['id'],'categoryId':regular['id']}
check('duplicate/shared-contact review required',admin.req('/members','POST',manual)[0]==409);manual['duplicateDecision']='CreateSeparate';family=good(admin,'/members','POST',manual);check('shared family mobile allowed without identity merge',family['personId']!=person_id)
# Suspensions and transfers revoke only membership-managed grants; independent officer roles remain separate.
check('invalid membership transition rejected',admin.req('/members/'+membership_id+'/status','POST',{'status':'Approved','reason':'Invalid test transition','version':member['version']})[0]==409)
suspended=good(admin,'/members/'+membership_id+'/status','POST',{'status':'Suspended','reason':'Reviewing membership eligibility','version':member['version']});check('membership suspension revokes managed workspace',applicant.req('/organizations')[0]==403)
active=good(admin,'/members/'+membership_id+'/status','POST',{'status':'Active','reason':'Eligibility review complete','version':suspended['version']});aw=next(w for w in applicant.req('/workspaces')[1] if w['orgId']==source['id']);applicant.activate(aw)
# A second affiliation references exactly the same global person.
b=good(applicant,'/membership/applications','POST',{'clubId':destination['id'],'categoryId':honorary['id'],'statement':'A second eligible affiliation.'});b=good(applicant,'/membership/applications/'+b['id']+'/submit','POST',{'version':b['version']});check('cross-club approval denied',secretary.req('/membership/applications/'+b['id']+'/decisions','POST',{'decision':'Approve','comment':'','version':b['version']})[0]==404)
for _ in range(2):b=good(admin,'/membership/applications/'+b['id']+'/decisions','POST',{'decision':'Approve','comment':'Association reviewer in configured stage.','version':b['version']})
check('multiple club memberships share one person',len(applicant.req('/membership/me')[1]['memberships'])==2 and int(sql(f'SELECT count(*) FROM "Persons" WHERE "UserId"=\'{applicant.req("/auth/me")[1]["id"]}\''))==1)
# Transfer the regular affiliation into a different category than the existing honorary affiliation.
transfer=good(applicant,'/members/'+membership_id+'/transfers','POST',{'destinationClubId':destination['id'],'categoryId':regular['id'],'reason':'Moving community service location','version':active['version']})
source_before=good(applicant,'/members/'+membership_id)['membership'];check('transfer request does not mutate current affiliation',source_before['status']=='Active')
transfer=good(admin,'/membership/transfers/'+transfer['id']+'/decisions','POST',{'decision':'Approve','comment':'Source review complete.','version':transfer['version']});check('source approval does not activate destination',transfer['status']=='PendingDestination')
check('source-only officer cannot approve destination stage',secretary.req('/membership/transfers/'+transfer['id']+'/decisions','POST',{'decision':'Approve','comment':'','version':transfer['version']})[0]==403)
transfer=good(admin,'/membership/transfers/'+transfer['id']+'/decisions','POST',{'decision':'Approve','comment':'Destination review complete.','version':transfer['version']});check('effective transfer completed',transfer['status']=='Completed')
portal=good(applicant,'/membership/me');source_history=next(x for x in portal['memberships'] if x['id']==membership_id);dest_member=next(x for x in portal['memberships'] if x['id']==transfer['destinationMembershipId']);check('transfer preserves person and source history',source_history['status']=='Transferred' and dest_member['personId']==person_id and int(sql(f'SELECT count(*) FROM "MembershipHistory" WHERE "MembershipId"=\'{membership_id}\''))>=4)
check('source membership permission reevaluated after transfer',not any(w['orgId']==source['id'] and w['role']=='Club Member' for w in applicant.req('/workspaces')[1]))
# Rejected transfer leaves the source active.
dest_w=next(w for w in applicant.req('/workspaces')[1] if w['orgId']==destination['id']);applicant.activate(dest_w)
t=good(applicant,'/members/'+dest_member['id']+'/transfers','POST',{'destinationClubId':source['id'],'categoryId':regular['id'],'reason':'A request that will be rejected','version':dest_member['version']});t=good(admin,'/membership/transfers/'+t['id']+'/decisions','POST',{'decision':'Reject','comment':'Transfer is not appropriate at this time.','version':t['version']});check('transfer rejection preserves active membership',t['status']=='Rejected' and applicant.req('/members/'+dest_member['id'])[1]['membership']['status']=='Active')
# Rejected application does not become an affiliation.
c=good(applicant,'/membership/applications','POST',{'clubId':source['id'],'categoryId':honorary['id'],'statement':'Rejection scenario'});c=good(applicant,'/membership/applications/'+c['id']+'/submit','POST',{'version':c['version']});good(secretary,'/membership/applications/'+c['id']+'/decisions','POST',{'decision':'Reject','comment':'Requirements not satisfied.','version':c['version']});check('rejected application has no membership',sql(f'SELECT count(*) FROM "Memberships" WHERE "SourceApplicationId"=\'{c["id"]}\'')=='0')
# Cross-tenant IDOR, even when the global person identity is shared, is denied.
check('cross-tenant club parameter denied',admin.req('/members','POST',{**manual,'clubId':other['orgId']})[0]==403)
admin.activate(other);other_opts=good(admin,'/membership/options');other_club=other_opts['clubs'][0];other_category=other_opts['categories'][0]
b=good(applicant,'/membership/applications','POST',{'clubId':other_club['id'],'categoryId':other_category['id'],'statement':'An affiliation in another eligible association.'});b=good(applicant,'/membership/applications/'+b['id']+'/submit','POST',{'version':b['version']})
for _ in range(2):b=good(admin,'/membership/applications/'+b['id']+'/decisions','POST',{'decision':'Approve','comment':'Configured association review.','version':b['version']})
foreign=next(m for m in applicant.req('/membership/me')[1]['memberships'] if m['tenantId']==other['tenantId']);admin.activate(root);check('cross-tenant member detail denied',admin.req('/members/'+foreign['id'])[0]==404);check('cross-club private member detail denied',secretary.req('/members/'+dest_member['id'])[0]==404)
# Account activation links require a verified account AND an explicit officer identity review.
linked,link_email=signup('Imported Account Owner '+uuid.uuid4().hex[:8]);link_invite=good(admin,'/membership/invitations','POST',{'clubId':source['id'],'categoryId':regular['id'],'email':link_email,'personId':family['personId'],'usageLimit':1});link=good(linked,'/membership/invitations/accept','POST',{'token':link_invite['link'].split('token=')[1]});check('account linking waits for identity review',linked.req('/membership/me')[1]['profile'] is None)
good(admin,'/membership/link-requests/'+link['id']+'/decide','POST',{'decision':'Approve','reason':'Officer verified identity with the intended applicant.'},204);check('reviewed account links to imported person',linked.req('/membership/me')[1]['profile']['id']==family['personId'])
# Private documents use authenticated endpoints, file validation, and explicit retention.
pdf=b'%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF';status,doc=applicant.upload('/members/'+dest_member['id']+'/documents','evidence.pdf',pdf,{'kind':'Identity'},'application/pdf');check('secure document upload',status==200 and doc['status']=='Pending',doc)
check('document cannot be downloaded anonymously',Client().request('/membership/documents/'+doc['id'])[0]==401);check('cross-club private document access denied',secretary.request('/membership/documents/'+doc['id'])[0]==404);check('owner document download',applicant.request('/membership/documents/'+doc['id'])[0]==200)
check('unsafe upload rejected',applicant.upload('/members/'+dest_member['id']+'/documents','danger.html',b'<script>alert(1)</script>',{'kind':'Identity'},'text/html')[0]==400)
# CSV and XLSX exports are permission-scoped and protect spreadsheet formulas.
injection=good(admin,'/members','POST',{'duplicateDecision':'CreateSeparate','person':{'legalName':'=2+2','displayName':'=2+2','contacts':[]},'clubId':source['id'],'categoryId':honorary['id']})
s,body,h=admin.request('/membership/export','POST',json.dumps({'format':'csv','columns':['name','email','phone'],'selectedIds':[injection['id'],dest_member['id']]}).encode(),{'Content-Type':'application/json'});check('CSV formula injection escaped',s==200 and "'=2+2" in body.decode('utf-8-sig'))
rows=list(csv.DictReader(io.StringIO(body.decode('utf-8-sig'))));check('export applies phone privacy',all(row.get('phone','')=='' for row in rows))
s,body,h=admin.request('/membership/export','POST',json.dumps({'format':'xlsx','columns':['name','club'],'selectedIds':[injection['id']]}).encode(),{'Content-Type':'application/json'});check('Excel export is a real workbook',s==200 and zipfile.is_zipfile(io.BytesIO(body)))
# Scoped filtering, stable pagination, metrics and saved views.
first=good(admin,'/members?sort=number&size=2');second=good(admin,'/members?sort=number&size=2&after='+urllib.parse.quote(first['nextCursor']));check('stable large-list cursor pagination',not(set(x['id'] for x in first['items'])&set(x['id'] for x in second['items'])))
check('member search',any(x['personId']==person_id for x in good(admin,'/members?search='+urllib.parse.quote(name))['items']))
metrics=good(admin,'/membership/metrics');expected=int(sql(f'SELECT count(DISTINCT "PersonId") FROM "Memberships" WHERE "TenantId"=\'{root["tenantId"]}\''));check('dashboard aggregation matches PostgreSQL truth',metrics['totalMembers']==expected,(metrics['totalMembers'],expected));check('retention cohort excludes new people',metrics['retained']<=metrics['beginning'])
good(admin,'/membership/views','POST',{'name':'Active test '+uuid.uuid4().hex[:6],'configuration':json.dumps({'grid':{'order':['name','club'],'hidden':[],'widths':{'name':250}},'statuses':['Active']})});check('saved grid views persisted',len(admin.req('/membership/views')[1])>0)
print('Phase 2 basic suite:',len(results),'checks passed')
