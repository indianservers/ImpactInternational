"""API integration tests against a real migrated PostgreSQL database. No mocks."""
import json,urllib.request,urllib.parse,urllib.error,http.cookiejar,os,time,statistics,uuid,pathlib
BASE=os.environ.get('CLUBS_TEST_URL','http://127.0.0.1:5081')+'/api/v1'
password=os.environ.get('CLUBS_SEED_PASSWORD') or pathlib.Path('.infra/seed-password').read_text()
results=[]
# The direct database checks below deliberately target only the isolated clubs_test database.
import subprocess,concurrent.futures,hashlib
PSQL=os.environ.get('CLUBS_PSQL',str(pathlib.Path('.infra/pgsql/bin/psql.exe').resolve()))
def sql(query):
 return subprocess.check_output([PSQL,'-h','127.0.0.1','-p','55432','-U','clubs','-d',os.environ.get('CLUBS_TEST_DATABASE','clubs_test'),'-X','-At','-v','ON_ERROR_STOP=1','-c',query],text=True).strip()

class Client:
 def __init__(self):
  self.jar=http.cookiejar.CookieJar();self.http=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar));self.csrf=self.req('/auth/csrf')[1]['token']
 def req(self,path,method='GET',data=None):
  r=urllib.request.Request(BASE+path,data=None if data is None else json.dumps(data).encode(),method=method,headers={'Content-Type':'application/json','X-CSRF':getattr(self,'csrf','')})
  try:
   with self.http.open(r) as x: body=x.read();return x.status,json.loads(body) if body else None
  except urllib.error.HTTPError as e:
   body=e.read()
   try:return e.code,json.loads(body)
   except:return e.code,body.decode()
 def login(self,email):return self.req('/auth/login','POST',{'email':email,'password':password})
def check(name,condition,detail=None):
 results.append({'test':name,'passed':bool(condition)});print(('PASS ' if condition else 'FAIL ')+name, '' if condition else detail);assert condition,(name,detail)
a=Client();check('anonymous API denied',a.req('/organizations')[0]==401)
check('admin login',a.login('admin@example.test')[0]==200)
ws=a.req('/workspaces')[1];check('multiple authorized workspaces',len(ws)>=3)
w=next(x for x in ws if x['role']=='Association Super Admin' and x['association'].startswith('Global'))
check('workspace activation',a.req('/workspaces/'+w['id']+'/activate','POST',{})[0]==204)
s,data=a.req('/organizations?size=2');check('server pagination',s==200 and len(data['items'])==2 and data['total']>=8,data)
s,dash=a.req('/dashboard');check('live dashboard',s==200 and dash['clubs']>=3,dash)
levels=a.req('/levels')[1];club_level=next(x for x in levels if x['isClub']);orgs=a.req('/organizations?size=100')[1]['items'];zone=next(x for x in orgs if x['levelId']==club_level['parentLevelId']);club=next(x for x in orgs if x['levelId']==club_level['id'])
r={'name':'Integration Community Club','code':'TEST-'+uuid.uuid4().hex[:8],'levelId':club_level['id'],'parentId':zone['id'],'city':'Test City','country':'India','status':'Active'}
s,created=a.req('/organizations','POST',r);check('organization creation',s==200,created)
check('duplicate code rejected',a.req('/organizations','POST',r)[0]==409)
r['code']='BAD-'+uuid.uuid4().hex[:8];r['parentId']=w['orgId'];check('invalid parent type rejected',a.req('/organizations','POST',r)[0]==400)
created['name']='Updated '+r['code'];s,updated=a.req('/organizations/'+created['id'],'PUT',created);check('organization update',s==200,updated)
check('stale update rejected',a.req('/organizations/'+created['id'],'PUT',created)[0]==409)
s,found=a.req('/organizations?search='+urllib.parse.quote(created['name']));check('organization search',s==200 and found['total']==1,found)
other=next(x for x in ws if x['association']=='Civic Futures Network');check('cross-tenant detail denied',a.req('/organizations/'+other['orgId'])[0]==404)
r['parentId']=other['orgId'];check('cross-tenant parent denied',a.req('/organizations','POST',r)[0]==403)
manager=Client();check('manager login',manager.login('manager@example.test')[0]==200);mw=next(w for w in manager.req('/workspaces')[1] if w['role']=='Club Manager');manager.req('/workspaces/'+mw['id']+'/activate','POST',{})
check('manager cannot modify sibling',manager.req('/organizations/'+created['id'],'PUT',updated)[0] in (403,404))
own=manager.req('/organizations/'+mw['orgId'])[1];old_name=own['name'];own['name']='Unauthorized name';own['status']='Archived'
check('archive requires atomic permission',manager.req('/organizations/'+own['id'],'PUT',own)[0]==403)
check('denied mutation does not persist',manager.req('/organizations/'+own['id'])[1]['name']==old_name)
check('manager cannot manage roles',manager.req('/roles','POST',{'name':'Escalated','permissions':['roles.manage']})[0]==403)
check('workspace id tampering denied',manager.req('/workspaces/'+w['id']+'/activate','POST',{})[0]==403)
member=Client();member.login('member@example.test');bw=member.req('/workspaces')[1][0];member.req('/workspaces/'+bw['id']+'/activate','POST',{})
check('member write denied',member.req('/organizations','POST',r)[0]==403)
check('member admin denied',member.req('/roles')[0]==403)
check('role assignment',a.req('/assignments','POST',{'email':'member@example.test','roleId':next(x for x in a.req('/roles')[1] if x['name']=='Club Member')['id'],'orgId':created['id']})[0]==204)
s,role=a.req('/roles','POST',{'name':'Scoped Assigner '+uuid.uuid4().hex[:8],'permissions':['organization.view','roles.view','roles.assign']})
check('role creation',s==200,role)
role['name']+=' updated';check('unassigned role editing',a.req('/roles/'+role['id'],'PUT',role)[0]==204)
check('scoped role assignment',a.req('/assignments','POST',{'email':'member@example.test','roleId':role['id'],'orgId':created['id']})[0]==204)
scoped=next(x for x in member.req('/workspaces')[1] if x['role']==role['name']);member.req('/workspaces/'+scoped['id']+'/activate','POST',{})
super_role=next(x for x in a.req('/roles')[1] if x['name']=='Association Super Admin')
check('assignment cannot escalate privileges',member.req('/assignments','POST',{'email':'member@example.test','roleId':super_role['id'],'orgId':created['id']})[0]==403)
check('assigned role editing protected',a.req('/roles/'+role['id'],'PUT',role)[0]==400)
check('association provisioning',a.req('/associations','POST',{'name':'Integration Association '+uuid.uuid4().hex[:8]})[0]==200)
check('non-platform provisioning denied',member.req('/associations','POST',{'name':'Unauthorized Association'})[0]==403)
check('registration request',a.req('/registration-requests','POST',{'name':'Requested Community','email':'requester@example.test','description':'Fictional integration request'})[0]==202)
check('platform registration review',a.req('/registration-requests')[0]==200)
request=a.req('/registration-requests')[1][0]
check('registration provisioning',a.req('/registration-requests/'+request['id']+'/provision','POST',{})[0]==200)
check('registration provisioning idempotency',a.req('/registration-requests/'+request['id']+'/provision','POST',{})[0]==409)
assignment=next(x for x in a.req('/assignments?roleId='+role['id'])[1] if x['role']==role['name'])
check('scoped role revocation',a.req('/assignments/'+assignment['id'],'DELETE',{})[0]==204)
check('revoked workspace denied',member.req('/dashboard')[0]==403)

check('registration review denied to member',member.req('/registration-requests')[0]==403)
check('audit generated',any(x['action']=='organization.update' for x in a.req('/audit-logs')[1]['items']))
new=Client();email='test-'+uuid.uuid4().hex+'@example.test';reg={'name':'Test User','email':email,'phone':'+910000000000','password':password,'confirmation':password,'agreed':True}
check('registration',new.req('/auth/register','POST',reg)[0]==202)
check('unverified login denied',new.login(email)[0]==401)
check('verification can be resent',new.req('/auth/resend-verification','POST',{'email':email})[0]==202)
mail=next(x for x in new.req('/development/mail')[1] if x['email']==email);token=mail['link'].split('token=')[1].split('&')[0]
check('email verification',new.req('/auth/complete','POST',{'token':token,'password':''})[0]==200)
check('verification replay denied',new.req('/auth/complete','POST',{'token':token,'password':''})[0]==400)
check('verified login',new.login(email)[0]==200)
check('signup grants no workspace',new.req('/workspaces')[1]==[])
check('forgot password',new.req('/auth/forgot-password','POST',{'email':email})[0]==202)
mail=next(x for x in new.req('/development/mail')[1] if x['email']==email);token=mail['link'].split('token=')[1].split('&')[0]
check('password reset',new.req('/auth/complete','POST',{'token':token,'password':password+'x'})[0]==200)
check('reset revokes sessions',new.req('/auth/me')[0]==401)
new.req('/auth/login','POST',{'email':email,'password':password+'x'});sessions=new.req('/auth/sessions')[1]
check('session revoke',new.req('/auth/sessions/'+sessions[0]['id'],'DELETE',{})[0]==204)
check('revoked session denied',new.req('/auth/me')[0]==401)
for _ in range(5):new.req('/auth/login','POST',{'email':email,'password':'incorrect-password'})
check('account lockout enforced',new.req('/auth/login','POST',{'email':email,'password':password+'x'})[0]==401)
check('logout',member.req('/auth/logout','POST',{})[0]==204)
check('logout invalidates session',member.req('/auth/me')[0]==401)
# Actual simultaneous writes must yield exactly one winner.
c1=Client();c2=Client()
for c in (c1,c2):c.login('admin@example.test');c.req('/workspaces/'+w['id']+'/activate','POST',{})
latest=a.req('/organizations/'+created['id'])[1]
def concurrent_save(c):return c.req('/organizations/'+created['id'],'PUT',latest)[0]
with concurrent.futures.ThreadPoolExecutor(2) as pool:codes=list(pool.map(concurrent_save,(c1,c2)))
check('concurrent writes have one winner',sorted(codes)==[200,409],codes)
# CSRF enforcement is independent of SameSite cookie protection.
raw=urllib.request.Request(BASE+'/auth/logout',data=b'{}',method='POST',headers={'Content-Type':'application/json'})
try:a.http.open(raw);csrf_status=200
except urllib.error.HTTPError as e:csrf_status=e.code
check('missing CSRF token rejected',csrf_status==400)
check('migration history applied',int(sql('SELECT count(*) FROM "__EFMigrationsHistory"'))>=6)
# Direct SQL attempts verify that the database itself enforces tenant and cycle constraints.
for name,query in [('database cross-tenant parent constraint',f'UPDATE "Orgs" SET "ParentId"=\'{other["orgId"]}\' WHERE "Id"=\'{created["id"]}\''),('database self-cycle rejected',f'UPDATE "Orgs" SET "ParentId"="Id" WHERE "Id"=\'{created["id"]}\'')]:
 process=subprocess.run([PSQL,'-h','127.0.0.1','-p','55432','-U','clubs','-d',os.environ.get('CLUBS_TEST_DATABASE','clubs_test'),'-X','-v','ON_ERROR_STOP=1','-c',query],capture_output=True,text=True)
 check(name,process.returncode!=0)
# A real server-side expiry check, rather than merely removing the browser cookie.
raw_session=next(cookie.value for cookie in c1.jar if cookie.name=='clubs_session');sid=hashlib.sha256(raw_session.encode()).hexdigest().upper()
sql(f'UPDATE "Sessions" SET "ExpiresAt"=now()-interval \'1 minute\' WHERE "Id"=\'{sid}\'')
check('expired session denied',c1.req('/auth/me')[0]==401)
check('OpenAPI document available',urllib.request.urlopen(BASE.replace('/api/v1','')+'/openapi/v1.json').status==200)
# Warm connection pool and compiled query before timings. Sequential single-user workload.
a.req('/organizations');timings=[]
for _ in range(100):
 start=time.perf_counter();s,_=a.req('/organizations?size=20');assert s==200;timings.append((time.perf_counter()-start)*1000)
perf={'samples':len(timings),'median_ms':round(statistics.median(timings),2),'p95_ms':round(sorted(timings)[94],2),'conditions':'Loopback HTTP, warm EF/query/connection pool, sequential requests, development seed plus integration-created rows; not a scale test.'}
pathlib.Path('test-results').mkdir(exist_ok=True);pathlib.Path('test-results/api.json').write_text(json.dumps({'tests':results,'performance':perf},indent=2));print(json.dumps(perf));print(f'{len(results)} tests passed')

