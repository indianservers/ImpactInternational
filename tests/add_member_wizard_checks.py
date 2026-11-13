"""Add Member wizard API regression. Writes only disposable fixtures in the QA database."""
import base64,copy,json,os,pathlib,struct,uuid,zlib,concurrent.futures
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE']=='clubs_impact_dues_upgrade_20261113'
results=[];tag=uuid.uuid4().hex[:8]
def check(name,ok,detail=None):
 results.append(dict(scenario=name,passed=bool(ok),detail=detail))
 if not ok:print('FAIL',name,str(detail)[:400],flush=True)
def req(c,name,path,body=None,status=200,method=None):
 s,d=c.req(path,method or ('POST' if body is not None else 'GET'),body);check(name,s==status,dict(status=s,response=d if s>=400 else None));return d
admin=Client();check('Admin sign in',admin.login('saisatish@indianservers.com')[0]==200)
clubadmin=Client();check('Club president sign in',clubadmin.login('club.president@example.test')[0]==200)
member=Client();check('Member sign in',member.login('member.club1@example.test')[0]==200)
opts=req(admin,'Options reuse','/membership/options');club=opts['clubs'][0]['id'];category=opts['categories'][0]['id'];foreign=next(x['id'] for x in opts['clubs'] if 'Region 02' in x['name'])
def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
photo=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',16,16,8,2,0,0,0))+chunk(b'IDAT',zlib.compress((b'\x00'+bytes([90,55,140])*16)*16))+chunk(b'IEND',b'')
pathlib.Path('.infra/wizard-qa-photo.png').write_bytes(photo)
person=dict(legalName='Wizard QA '+tag,displayName='Wizard Display '+tag,birthDate='1990-02-03',gender='Female',anniversary='2015-04-05',maritalStatus='Married',spouseName='QA Spouse',languages=['English','Telugu'],company='QA Employer',designation='QA Lead',industry='Education',profession='Trainer',businessCategory='Training',workLocation='Hyderabad',website='https://example.test',networking='Mentoring',educationLevel='Graduate',qualification='BSc',institution='QA College',employmentStatus='Employed',additionalInformation='Wizard notes',skills=['Public speaking'],expertise=['Training'],interests=['Service'],socialLinks={'LinkedIn':'@wizard-qa'},contacts=[dict(kind='Phone',label='Primary mobile',value='+91 9000000001'),dict(kind='Phone',label='Alternative mobile',value='+91 9000000002'),dict(kind='Email',label='Primary email',value=f'wizard-{tag}@example.test'),dict(kind='Email',label='Alternative email',value=f'wizard-alt-{tag}@example.test'),dict(kind='Emergency',label='Emergency contact',value='QA Contact +91 9000000003')],addresses=[dict(country='India',state='Telangana',city='Hyderabad',postalCode='500001',address='QA Residential Address',type='Residential',isPrimary=False),dict(country='India',state='Telangana',city='Secunderabad',postalCode='500003',address='QA Office Address',type='Office',isPrimary=True)])
body=dict(person=person,clubId=club,categoryId=category,joinedAt='2026-11-01T00:00:00Z',photoBase64=base64.b64encode(photo).decode(),status='Inactive',duplicateDecision='CreateSeparate',requestKey=str(uuid.uuid4()))
created=req(admin,'Full registration with inactive status','/members',body)
assert 'id' in created,created
mid=created['id'];detail=req(admin,'Read registered profile','/members/'+mid);p=detail['person']
for k in ['legalName','birthDate','gender','anniversary','maritalStatus','spouseName','educationLevel','qualification','institution','employmentStatus','additionalInformation']:
 check('Persist '+k,p[k]==person[k],p.get(k))
check('Display name saved',p['name']==person['displayName']);check('All contacts saved',len(p['contacts'])==5)
check('Multiple addresses retain type and primary',len(p['addresses'])==2 and next(x for x in p['addresses'] if x['isPrimary'])['type']=='Office',p['addresses'])
check('Initial status saved',detail['membership']['status']=='Inactive');check('Generated ID unchanged',created['number'].startswith('IM-'))
check('Photo saved',p['hasPhoto']);check('Profession saved',sql(f'''SELECT "Profession" FROM "Persons" WHERE "Id"='{created["personId"]}' ''' )=='Trainer' );check('Tags saved',sql(f'''SELECT "Value" FROM "PersonTags" WHERE "PersonId"='{created["personId"]}' AND "Kind"='Skill' ''')=='Public speaking' );check('Languages saved',p['languages']==['English','Telugu']);check('Social links normalized',p['socialLinks']['LinkedIn'].startswith('https://'))
row=req(admin,'Directory uses primary address','/members?search='+created['number']);check('Primary city displayed',row['items'][0]['city']=='Secunderabad')
replay=req(admin,'Same request safely replayed','/members',body);check('Replay returns same member',replay==created)
changed=copy.deepcopy(body);changed['person']['displayName']='Changed';req(admin,'Changed payload with same key rejected','/members',changed,409)
check('Replay creates one membership',sql(f'''SELECT count(*) FROM "Memberships" WHERE "PersonId"='{created['personId']}' ''')=='1')
for name,mutate in [('Two primary addresses',lambda b:b['person']['addresses'][0].update(isPrimary=True)),('Unsupported address type',lambda b:b['person']['addresses'][0].update(type='Invalid')),('Invalid initial status',lambda b:b.update(status='Expired')),('Invalid PIN',lambda b:b['person']['addresses'][0].update(postalCode='abc'))]:
 b=copy.deepcopy(body);b['requestKey']=str(uuid.uuid4());b['person']['legalName']='Invalid '+tag;b['duplicateDecision']='CreateSeparate';mutate(b);req(admin,name,'/members',b,400)
legacy=dict(person=dict(legalName='Wizard Legacy '+tag,displayName='',addresses=[dict(country='India',state='',city='Delhi',postalCode='110001',address='Legacy address')]),clubId=club,categoryId=category)
l=req(admin,'Legacy creation payload supported','/members',legacy);ld=req(admin,'Legacy address profile','/members/'+l['id']);check('Legacy address default primary Residential',ld['person']['addresses'][0]['isPrimary'] and ld['person']['addresses'][0]['type']=='Residential')
edit=ld['person'];edit['addresses'].append(dict(country='India',state='',city='Mumbai',postalCode='400001',address='New office',type='Office',isPrimary=True));edit['addresses'][0]['isPrimary']=False
req(admin,'Existing profile address update','/members/'+l['id']+'/person',edit,204,'PUT')
check('Primary address update saved',next(x for x in admin.req('/members/'+l['id'])[1]['person']['addresses'] if x['isPrimary'])['city']=='Mumbai')
b=dict(person=dict(legalName='Wizard Forbidden '+tag,displayName=''),clubId=foreign,categoryId=category,requestKey=str(uuid.uuid4()))
req(clubadmin,'Unauthorized club assignment rejected','/members',b,403);req(member,'Ordinary member cannot register','/members',b,403)
req(clubadmin,'Foreign hierarchy path rejected','/membership/hierarchy?clubId='+foreign,status=404)
# Concurrent retries are serialized inside the existing workspace mutation transaction.
b=dict(person=dict(legalName='Wizard Concurrent '+tag,displayName=''),clubId=club,categoryId=category,requestKey=str(uuid.uuid4()))
c2=Client();c2.login('saisatish@indianservers.com')
with concurrent.futures.ThreadPoolExecutor(2) as ex: replies=list(ex.map(lambda c:c.req('/members','POST',b),[admin,c2]))
check('Concurrent retries return same membership',all(s==200 for s,d in replies) and replies[0][1]['id']==replies[1][1]['id'],replies)
check('Concurrent retry count one',sql(f'''SELECT count(*) FROM "Persons" WHERE "LegalName"='Wizard Concurrent {tag}' ''')=='1')
matches=req(admin,'Duplicate review endpoint retained','/members/duplicates',person);check('Duplicate candidates found',any(x['id']==created['personId'] for x in matches))
b=copy.deepcopy(body);b['requestKey']=str(uuid.uuid4());b['duplicateDecision']='';req(admin,'Unreviewed duplicate rejected','/members',b,409)
s,d=admin.upload('/members/'+mid+'/documents','wizard-photo.png',photo,{'kind':'Supporting'},'image/png');check('Existing document upload',s==200,d)
check('No incomplete member from invalid requests',sql(f'''SELECT count(*) FROM "Persons" WHERE "LegalName"='Invalid {tag}' ''')=='0')
pathlib.Path('docs/qa/add-member-wizard-api-results.json').write_text(json.dumps(dict(total=len(results),passed=sum(x['passed'] for x in results),results=results,member=created),indent=2))
print(json.dumps(dict(total=len(results),passed=sum(x['passed'] for x in results),failed=[x for x in results if not x['passed']]),indent=2))
assert all(x['passed'] for x in results)
