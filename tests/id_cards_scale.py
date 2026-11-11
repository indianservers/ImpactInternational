"""Measured real durable 1k/10k card jobs in an isolated synthetic scale database."""
import os,sys,time,json,pathlib,subprocess,statistics,urllib.request,concurrent.futures,hashlib,zipfile,psutil
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5088'
from support import Client,sql,BASE
DB='clubs_idcards_scale';out=pathlib.Path('test-results/idcard-scale');out.mkdir(exist_ok=True)
for _ in range(180):
 try:
  if sql('SELECT count(*) FROM "__EFMigrationsHistory"',DB)>='36':urllib.request.urlopen(BASE+'/health/ready',timeout=2);break
 except:time.sleep(1)
else:raise RuntimeError('Scale database/API not ready')
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code in (200,204),(p,code,b);return b
admin=Client();assert admin.login('admin@example.test')[0]==200;w=next(x for x in good(admin,'/workspaces') if x['association'].startswith('Global') and x['role']=='Association Super Admin');admin.activate(w)
reviewId=sql("SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='secretary@example.test'",DB)
# Isolated synthetic fixture grants the independent test reviewer an existing root role.
sql(f"INSERT INTO \"Assignments\" SELECT (jsonb_populate_record(NULL::\"Assignments\",to_jsonb(a)||jsonb_build_object('Id',gen_random_uuid(),'UserId','{reviewId}'))).* FROM \"Assignments\" a WHERE a.\"Id\"='{w['id']}' AND NOT EXISTS(SELECT 1 FROM \"Assignments\" x WHERE x.\"UserId\"='{reviewId}' AND x.\"OrgId\"=a.\"OrgId\" AND x.\"RoleId\"=a.\"RoleId\")",DB)
review=Client();assert review.login('secretary@example.test')[0]==200;rw=next(x for x in good(review,'/workspaces') if x['orgId']==w['orgId'] and x['role']=='Association Super Admin');review.activate(rw)
options=good(admin,'/id-cards/options');design=options['design'];template=good(admin,'/id-cards/templates','POST',{'orgId':w['orgId'],'name':'Measured 300 DPI two-sided scale '+str(int(time.time())),'design':design});v=good(admin,'/id-cards/templates/'+template['id']+'/publish','POST',{})
ids=sql(f'''SELECT m."Id" FROM "Memberships" m WHERE m."TenantId"='{w["tenantId"]}' AND (m."EndsAt" IS NULL OR m."EndsAt">now()) AND m."Number" LIKE 'GOV-SCALE-%' AND m."Status"='Active' AND NOT EXISTS(SELECT 1 FROM "DigitalCards" c WHERE c."MembershipId"=m."Id" AND c."State" IN ('Legacy','Issued','Generated') AND c."RevokedAt" IS NULL) ORDER BY m."Number" LIMIT 11000''',DB).splitlines();assert len(ids)==11000
pid=next(c.pid for c in psutil.net_connections(kind='tcp') if c.laddr.port==5088 and c.status=='LISTEN');process=psutil.Process(pid)
report={'conditions':'Real PostgreSQL, isolated cloned synthetic database, one bounded durable worker, standard CR80 front/back design, 300 DPI PNGs, two-page vector PDF, QR on back; no member photographs in synthetic fixture; shared development workstation. Timings include native rendering, private files, database writes and final ZIP packaging.','database':DB,'people':int(sql('SELECT count(*) FROM "Persons"',DB)),'clubs':int(sql('SELECT count(*) FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."IsClub"',DB)),'jobs':[]}
if '--large' in sys.argv:
 previous=json.loads(pathlib.Path('docs/idcard-scale.json').read_text());report['jobs']=previous['jobs']
for count,start in ([(10000,0)] if '--large' in sys.argv else [(1000,0),(10000,1000)]):
 chosen=ids[start:start+count];begun=time.perf_counter();cpu=process.cpu_times();peak=process.memory_info().rss;submitted=good(admin,'/id-cards/bulk-jobs','POST',{'templateVersionId':v['id'],'selection':{'orgId':w['orgId'],'membershipIds':chosen,'reissue':False},'format':'Zip'});r=submitted['request'];job=submitted['job'];assert job['total']==count,('Selection eligibility changed',job['total'],count);queued=time.perf_counter();good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent synthetic throughput measurement','version':r['version']});approved=time.perf_counter();last=0
 while True:
  state=good(admin,'/id-cards/bulk-jobs/'+job['id']);j=state['job'];peak=max(peak,process.memory_info().rss)
  if j['success']+j['failed']-last>=250:print(count,j['success'],j['failed'],j['status'],flush=True);last=j['success']+j['failed']
  if j['status'] in ['Completed','Completed with errors','Failed','Cancelled']:break
  assert time.perf_counter()-approved<7200,'Benchmark deadline exceeded'
  time.sleep(2)
 completed=time.perf_counter();assert j['status']=='Completed' and j['success']==count and j['failed']==0,j
 target=out/(str(count)+'-cards.zip');download=time.perf_counter();request=urllib.request.Request(BASE+'/api/v1/id-cards/bulk-jobs/'+job['id']+'/download')
 with admin.http.open(request,timeout=120) as response,target.open('wb') as file:
  while chunk:=response.read(262144):file.write(chunk)
 downloaded=time.perf_counter()
 with zipfile.ZipFile(target) as archive:assert len(archive.namelist())==count*3+1;assert archive.testzip() is None
 after=process.cpu_times();result={'count':count,'jobId':job['id'],'submitSeconds':round(queued-begun,3),'approvalSeconds':round(approved-queued,3),'generationAndPackagingSeconds':round(completed-approved,3),'cardsPerSecond':round(count/(completed-approved),2),'downloadSeconds':round(downloaded-download,3),'zipBytes':target.stat().st_size,'zipEntries':count*3+1,'crcVerified':True,'apiPeakRssMiB':round(peak/1024**2,1),'apiCpuSeconds':round(after.user+after.system-cpu.user-cpu.system,2),'failed':0};report['jobs'].append(result);pathlib.Path('docs/idcard-scale.json').write_text(json.dumps(report,indent=2));print(json.dumps(result),flush=True)
# Protected concurrent downloads are read in bounded chunks; no archive retained per thread.
def download_one(_):
 start=time.perf_counter();h=hashlib.sha256();size=0
 with admin.http.open(BASE+'/api/v1/id-cards/bulk-jobs/'+job['id']+'/download',timeout=120) as response:
  while data:=response.read(262144):h.update(data);size+=len(data)
 return {'seconds':round(time.perf_counter()-start,3),'bytes':size,'sha256':h.hexdigest()}
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:downloads=list(pool.map(download_one,range(4)))
assert len({d['sha256'] for d in downloads})==1 and all(d['bytes']==target.stat().st_size for d in downloads);report['concurrentDownloads']={'concurrency':4,'sameBytes':True,'samples':downloads};pathlib.Path('docs/idcard-scale.json').write_text(json.dumps(report,indent=2));print('PASS measured 1k/10k generation and concurrent downloads',flush=True)
