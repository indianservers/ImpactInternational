"""Members redesign regression suite. Writes are limited to newly created QA fixtures."""
import base64,csv,datetime,io,json,os,pathlib,struct,sys,time,uuid,zlib
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_dues_upgrade_')
results=[];tag=uuid.uuid4().hex[:8];prefix='Members Redesign '+tag
def check(name,ok,detail=None):
 results.append(dict(scenario=name,passed=bool(ok),detail=detail))
 if not ok:print('FAIL',name,str(detail)[:250],flush=True)
def request(c,name,path,expected=200,method='GET',body=None):
 at=time.perf_counter();s,d=c.req(path,method,body);check(name,s in (expected if isinstance(expected,list) else [expected]),dict(status=s,elapsedMs=round((time.perf_counter()-at)*1000),response=d if s>=400 else None));return d
def org(code):return sql(f'''SELECT "Id" FROM "Orgs" WHERE "Code"='{code}' ''')
a=Client();check('Platform administrator login',a.login('saisatish@indianservers.com')[0]==200)
c=Client();check('Club president login',c.login('club.president@example.test')[0]==200)
member=Client();check('Ordinary member login',member.login('member.club1@example.test')[0]==200)
club=org('DEMO-R01-D01-Z01-C01');club2=org('DEMO-R01-D01-Z01-C02');foreign=org('DEMO-R02-D01-Z01-C01')
region=org('DEMO-R01');district=org('DEMO-R01-D01');zone=org('DEMO-R01-D01-Z01')
opts=request(a,'Existing options endpoint','/membership/options');category=opts['categories'][0]['id']
def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
photo=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',16,16,8,2,0,0,0))+chunk(b'IDAT',zlib.compress((b'\x00'+bytes([30,45,80])*16)*16))+chunk(b'IEND',b'')
created=[]
for i in range(4):
 person=dict(legalName=prefix+' '+str(i),displayName='',contacts=[dict(kind='Email',label='Personal',value=f'members-redesign-{tag}-{i}@example.test')])
 body=dict(person=person,clubId=club if i<3 else foreign,categoryId=category,photoBase64=base64.b64encode(photo).decode() if i==0 else None)
 d=request(a,'Create member '+str(i),'/members',200,'POST',body);created.append(d)
check('Generated member IDs unique',len({m['number'] for m in created})==4 and all(m['number'].startswith('IM-') for m in created))
mid=created[0]['id'];detail=request(a,'Profile read','/members/'+mid)
check('Profile photo persisted',detail['person']['hasPhoto'])
s,data,_=a.request('/members/'+mid+'/photo');check('Protected photo read',s==200 and data.startswith(b'\x89PNG'))
request(a,'Privacy-protected directory photo','/members/'+mid+'/photo?directory=true',404)
person=detail['person'];person['displayName']=prefix+' Updated';request(a,'Edit member profile','/members/'+mid+'/person',204,'PUT',person)
check('Profile edit persisted',a.req('/members/'+mid)[1]['person']['name']==prefix+' Updated')
for key,val in [('search',tag),('regionId',region),('districtId',district),('zoneId',zone),('clubId',club),('categoryId',category),('membershipType',opts['membershipTypes'][0])]:
 d=request(a,'Filter '+key,'/members?'+key+'='+val);check('Filtered results bounded '+key,len(d['items'])<=20)
request(a,'Combined hierarchy filters','/members?regionId='+region+'&districtId='+district+'&zoneId='+zone+'&clubId='+club+'&search='+tag)
d=request(a,'Search by generated member ID','/members?search='+created[0]['number']);check('Exact ID returns created member',d['total']==1 and d['items'][0]['id']==mid)
for sort in ['name','name-desc','number','number-desc','club','category','joined','joined-asc','renewal','status','status-desc','updated']:
 request(a,'Server sort '+sort,'/members?sort='+sort+'&size=5')
p1=request(a,'Pagination first page','/members?sort=number&size=5');p2=request(a,'Pagination second page','/members?sort=number&size=5&page=2');check('Pages do not overlap',not ({m['id'] for m in p1['items']}&{m['id'] for m in p2['items']}))
for i,state in [(1,'Inactive'),(2,'Suspended')]:
 d=a.req('/members/'+created[i]['id'])[1]
 request(a,'Bulk-equivalent '+state+' validates record','/members/'+created[i]['id']+'/status',200,'POST',dict(status=state,reason='Redesign QA status change',version=d['membership']['version']))
 request(a,'Stale version rejected '+state,'/members/'+created[i]['id']+'/status',409,'POST',dict(status='Active',reason='Stale QA request',version=d['membership']['version']))
for state in ['Active','Inactive','Suspended','Transferred','Expired','Renewal Due']:
 d=request(a,'Status tab '+state,'/members?statuses='+state.replace(' ','%20')+'&search='+tag)
 if state in ['Inactive','Suspended']:check('Status tab matches '+state,d['total']==1 and all(m['status']==state for m in d['items']))
d=a.req('/members/'+mid)[1];newEnd=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(days=730)).isoformat();key='redesign-renew-'+tag
request(a,'Renew member','/members/'+mid+'/renewals',200,'POST',dict(newEnd=newEnd,version=d['membership']['version'],requestKey=key))
request(a,'Renewal idempotency','/members/'+mid+'/renewals',200,'POST',dict(newEnd=newEnd,version=d['membership']['version'],requestKey=key))
today=datetime.date.today().isoformat()
request(a,'Joined date filter','/members?joinedFrom='+today+'&joinedTo='+today)
request(a,'Renewal date filter','/members?renewalFrom='+today+'&renewalTo=2030-12-31')
request(a,'Invalid joined range rejected','/members?joinedFrom=2026-12-01&joinedTo=2026-01-01',400)
request(a,'Invalid renewal range rejected','/members?renewalFrom=2030-01-01&renewalTo=2026-01-01',400)
if len(opts['categories'])>1:
 d=a.req('/members/'+mid)[1];request(a,'Assign category','/members/'+mid+'/category',204,'POST',dict(categoryId=opts['categories'][1]['id'],reason='Redesign category QA',version=d['membership']['version']))
d=a.req('/members/'+mid)[1];request(a,'Create reviewed club transfer','/members/'+mid+'/transfers',200,'POST',dict(destinationClubId=club2,categoryId=category,reason='Redesign transfer QA',version=d['membership']['version']))
view=dict(grid=dict(order=['name','number','club'],hidden=[],widths={}),regionId=region,districtId=district,zoneId=zone,view='cards',extra=dict(joinedFrom=today),sort='number-desc')
request(a,'Save filter view','/membership/views',200,'POST',dict(name='Redesign QA '+tag,configuration=json.dumps(view)))
saved=request(a,'Read saved views','/membership/views');check('Saved view preserves settings',any(v['name']=='Redesign QA '+tag and json.loads(v['configuration'])==view for v in saved))
request(c,'Saved views isolated by user','/membership/views');check('Other user cannot see private saved view',all(v['name']!='Redesign QA '+tag for v in c.req('/membership/views')[1]))
for fmt in ['csv','xlsx']:
 payload=dict(format=fmt,columns=['name','number','club','region','district','zone','offices','endsAt','status'],districtId=district,zoneId=zone,search=tag,sort='number-desc')
 s,data,_=a.request('/membership/export','POST',json.dumps(payload).encode(),{'Content-Type':'application/json'});check('Filtered '+fmt+' export',s==200 and (data.startswith(b'PK') if fmt=='xlsx' else prefix.encode() in data),s)
 if fmt=='csv':
  records=list(csv.DictReader(io.StringIO(data.decode('utf-8-sig'))));check('Export matches filters and hierarchy',len(records)==3 and all(r['district']=='Demo Region 01 District 01' for r in records))
s,data,_=a.request('/membership/export','POST',json.dumps(dict(format='csv',columns=['number'],selectedIds=[mid])).encode(),{'Content-Type':'application/json'});check('Export selected only',s==200 and len(data.decode('utf-8-sig').strip().splitlines())==2)
invite=request(a,'Create individual invitation','/membership/invitations',200,'POST',dict(clubId=club,categoryId=category,email='',message='QA invitation message',usageLimit=1))
invitations=request(a,'Track invitation status','/membership/invitations');check('Pending acceptance tracked',any(i['id']==invite['id'] and i['uses']==0 for i in invitations))
request(a,'Revoke test invitation','/membership/invitations/'+invite['id'],204,'DELETE')
for r in ['30d','3m','6m','12m']:
 m=request(a,'Membership intelligence '+r,'/membership/metrics?range='+r);check('Chart and status totals reconcile '+r,sum(g['count'] for g in m['intelligence']['statusCounts'])==m['memberships'] and sum(g['count'] for g in m['intelligence']['regions'])==m['memberships'])
request(a,'Custom intelligence dates','/membership/metrics?range=custom&from='+today+'&to='+today)
request(a,'Invalid intelligence range','/membership/metrics?range=bad',400)
request(a,'Future intelligence range','/membership/metrics?range=custom&to=2099-01-01',400)
# Ensure hierarchy filtering cannot widen an ordinary/scoped workspace.
for key,val in [('districtId',org('DEMO-R02-D01')),('zoneId',org('DEMO-R02-D01-Z01'))]:
 request(c,'Foreign '+key+' list denied','/members?'+key+'='+val,404)
 request(c,'Foreign '+key+' options denied','/membership/options?'+key+'='+val,404)
 request(c,'Foreign '+key+' export denied','/membership/export',404,'POST',dict(format='csv',**{key:val}))
request(c,'Other club profile direct URL denied','/members/'+created[3]['id'],404)
request(c,'Other club photo denied','/members/'+created[3]['id']+'/photo',404)
for path in ['/members','/membership/metrics','/membership/imports','/membership/views']:
 request(member,'Ordinary member administrative API denied '+path,path,403)
request(member,'Ordinary member cannot export','/membership/export',403,'POST',dict(format='csv'))
request(member,'Ordinary member directory uses privacy','/directory')
request(member,'Private profile direct URL denied','/members/'+mid,403)
request(member,'Private directory profile denied','/members/'+mid+'?directory=true',404)
# Existing import validation/duplicate/commit flow; rows remain in the QA DB for review.
def upload(name,content):
 s,d=a.upload('/membership/imports',name,content.encode(),dict(clubId=club,categoryId=category,requestKey='redesign-'+uuid.uuid4().hex),content_type='text/csv');check('Import upload '+name,s==200,s);return d
def waitJob(id):
 for _ in range(100):
  d=a.req('/membership/imports/'+id)[1]
  if d['status'] not in ['Validating','Queued','Importing','Uploaded']:return d
  time.sleep(.15)
 return d
job=upload('redesign-review.csv','name,email,joinedAt\n'+prefix+' duplicate,members-redesign-'+tag+'-0@example.test,\n'+prefix+' invalid,,not-a-date\n')
request(a,'Map import columns','/membership/imports/'+job['id']+'/mapping',202,'POST',dict(fields=dict(name='name',email='email',joinedAt='joinedAt')))
j=waitJob(job['id']);check('Import detects duplicates and invalid rows',j['totalReview']>=1 and j['totalErrors']>=1,dict(status=j['status'],errors=j['totalErrors'],review=j['totalReview']))
request(a,'Import blocked before valid review','/membership/imports/'+job['id']+'/commit',409,'POST',{})
request(a,'Cancel invalid staged import','/membership/imports/'+job['id']+'/cancel',204,'POST',{})
job=upload('redesign-valid.csv','name,email\n'+prefix+' imported,import-'+tag+'@example.test\n')
request(a,'Map valid import','/membership/imports/'+job['id']+'/mapping',202,'POST',dict(fields=dict(name='name',email='email')))
j=waitJob(job['id']);check('Valid import preview ready',j['status']=='Ready',j['status'])
request(a,'Confirm valid import','/membership/imports/'+job['id']+'/commit',202,'POST',{})
j=waitJob(job['id']);check('Import summary completed',j['status']=='Completed' and j['importedRows']==1,j['status'])
request(a,'Import commit idempotent','/membership/imports/'+job['id']+'/commit',200,'POST',{})
check('Import does not create duplicate records',a.req('/members?search='+tag)[1]['total']==5)
request(member,'Member import mutation forbidden','/membership/imports/'+job['id']+'/commit',403,'POST',{})
duplicate=request(a,'Duplicate identity preview','/members/duplicates',200,'POST',dict(legalName=prefix+' copy',displayName='',contacts=[dict(kind='Email',label='Personal',value='members-redesign-'+tag+'-0@example.test')]))
check('Duplicate identity requires explicit review',len(duplicate)>0)
output=dict(total=len(results),passed=sum(r['passed'] for r in results),failed=sum(not r['passed'] for r in results),database=os.environ['CLUBS_TEST_DATABASE'],fixtures=created,tag=tag,scenarios=results)
pathlib.Path('docs/qa/members-redesign-results.json').write_text(json.dumps(output,indent=2))
print(json.dumps({k:output[k] for k in ['total','passed','failed']}));sys.exit(1 if output['failed'] else 0)
