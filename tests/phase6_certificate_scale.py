"""Actual 50-recipient preparation and 50-PDF generation on the isolated synthetic scale DB."""
import os,time,json,pathlib,subprocess,urllib.request,statistics,io,zipfile,uuid
import support
DB='clubs_governance_scale';support.BASE='http://127.0.0.1:5087';sql=lambda q:support.sql(q,DB)
assert int(sql('SELECT count(*) FROM "VolunteerHours"'))>=250000,'Requires isolated Phase 6 scale fixture'
process=None
try:
 env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':f'Host=127.0.0.1;Port=55432;Database={DB};Username=clubs','Logging__LogLevel__Default':'Warning'};log=pathlib.Path('.infra/phase6-certificate-scale-api.log').open('w');process=subprocess.Popen(['dotnet','server/bin/Debug/net10.0/Clubs.Api.dll','--urls',support.BASE],env=env,stdout=log,stderr=log)
 for _ in range(100):
  try:urllib.request.urlopen(support.BASE+'/health',timeout=1);break
  except:time.sleep(.2)
 c=support.Client();assert c.login('admin@example.test')[0]==200;w=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(w)
 project=json.loads(sql('SELECT row_to_json(p) FROM "ServiceProjects" p WHERE EXISTS(SELECT 1 FROM "VolunteerHours" h WHERE h."ProjectId"=p."Id" AND h."Status"=\'Verified\') LIMIT 1'));people=json.loads(sql(f"SELECT json_agg(p) FROM(SELECT DISTINCT \"PersonId\" p FROM \"VolunteerHours\" WHERE \"ProjectId\"='{project['Id']}' AND \"Status\"='Verified' LIMIT 50) x"))
 def good(path,body):
  code,b=c.req(path,'POST',body);assert code==200,(path,code,b);return b
 template=good('/certificate-templates',{'orgId':project['OrgId'],'name':'SCALE recognition '+uuid.uuid4().hex[:8],'type':'Service recognition','layout':{'orientation':'Landscape','background':'#fffdf5','border':'#b39755','elements':[{'text':'Certificate of service','x':50,'y':20,'size':30},{'text':'{{member_name}}','x':50,'y':45,'size':32},{'text':'{{service_hours}} verified hours','x':50,'y':65,'size':18},{'text':'{{certificate_number}}','x':50,'y':82,'size':11}]}})
 start=time.perf_counter();batch=good('/certificates',{'orgId':project['OrgId'],'templateId':template['id'],'people':people,'sourceType':'Project','sourceId':project['Id']});prepare_ms=(time.perf_counter()-start)*1000;assert len(batch)==50
 # Benchmark rendering only: correctness suites separately exercise actual independent approval.
 ids=[x['id'] for x in batch];quoted=','.join("'"+x+"'" for x in ids);sql(f"UPDATE \"IssuedCertificates\" SET \"Status\"='Issued',\"IssuedAt\"=now(),\"Version\"=\"Version\"+1 WHERE \"Id\" IN({quoted})")
 samples=[];size=0
 for _ in range(5):
  start=time.perf_counter();code,b,h=c.request('/certificates/bulk-pdf','POST',json.dumps(ids).encode(),{'Content-Type':'application/json'});elapsed=(time.perf_counter()-start)*1000;assert code==200,(code,b[:100]);archive=zipfile.ZipFile(io.BytesIO(b));assert len(archive.namelist())==50 and all(archive.read(n).startswith(b'%PDF') for n in archive.namelist());size=len(b);samples.append(elapsed)
 result={'recipients':50,'prepare_ms':round(prepare_ms,2),'rendered_pdf_count_per_zip':50,'zip_bytes':size,'samples':5,'p50_ms':round(statistics.median(samples),2),'p95_ms':round(max(samples),2),'errors':0,'conditions':'Actual eligibility/placeholder checks and 50-row certificate preparation through API against synthetic verified-hour fixture. Rendering state is explicitly set by benchmark SQL only; real independent approvals are exercised separately in correctness suites. Five local 50-PDF ZIP requests, each parsed and all PDFs checked. Shared Windows machine, loopback API, no external delivery or production SLO.'};pathlib.Path('test-results/phase6-certificate-scale.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
finally:
 if process:process.terminate();process.wait(timeout=20)
