import os,time,json,pathlib,sys,hashlib,psutil
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5088';sys.path.insert(0,'tests')
from support import Client,sql
DB='clubs_idcards_scale'
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code in (200,204),(code,b);return b
admin=Client();assert admin.login('admin@example.test')[0]==200
w=next(x for x in good(admin,'/workspaces') if x['association'].startswith('Global') and x['role']=='Association Super Admin');admin.activate(w)
r=Client();assert r.login('secretary@example.test')[0]==200;r.activate(next(x for x in good(r,'/workspaces') if x['orgId']==w['orgId'] and x['role']=='Association Super Admin'))
old='bbcaad71-81b3-445e-afa5-5b7ba01369ae';version=sql(f'''SELECT "TemplateVersionId" FROM "IdCardJobs" WHERE "Id"='{old}' ''',DB)
ids=sql(f'''SELECT "MembershipId" FROM "IdCardJobItems" WHERE "JobId"='{old}' ORDER BY "Ordinal"''',DB).splitlines();assert len(ids)==1000
query=f'''SELECT md5(string_agg(c."Id"::text||c."State"||c."Files",',' ORDER BY c."Id")) FROM "DigitalCards" c WHERE c."MembershipId" IN (SELECT "MembershipId" FROM "IdCardJobItems" WHERE "JobId"='{old}')'''
before=sql(query,DB);start=time.perf_counter();job=good(admin,'/id-cards/bulk-jobs','POST',{'templateVersionId':version,'selection':{'orgId':w['orgId'],'membershipIds':ids},'format':'Print','reuseIssued':True});assert job['job']['total']==1000
req=job['request'];good(r,'/governance/requests/'+req['id']+'/decisions','POST',{'decision':'Approve','comment':'Review existing issued-card print export','version':req['version']})
pid=next(c.pid for c in psutil.net_connections(kind='tcp') if c.laddr.port==5088 and c.status=='LISTEN');proc=psutil.Process(pid);peak=0
while True:
 j=good(admin,'/id-cards/bulk-jobs/'+job['job']['id'])['job'];peak=max(peak,proc.memory_info().rss)
 if j['status'] in ['Completed','Completed with errors','Failed','Cancelled']:break
 assert time.perf_counter()-start<600
 time.sleep(1)
assert j['status']=='Completed' and j['success']==1000 and j['failed']==0,j
assert before==sql(query,DB),'Existing card records changed'
code,pdf,_=admin.request('/id-cards/bulk-jobs/'+j['id']+'/download');assert code==200
import pymupdf
p=pymupdf.open(stream=pdf,filetype='pdf');assert len(p)==250;assert abs(p[0].rect.width*25.4/72-210)<.01
report={'count':1000,'format':'A4 duplex PDF','pages':len(p),'seconds':round(time.perf_counter()-start,3),'bytes':len(pdf),'apiPeakRssMiB':round(peak/1024**2,1),'existingCardRecordsUnchanged':True,'failed':0}
pathlib.Path('test-results/idcard-scale/1000-print.pdf').write_bytes(pdf);pathlib.Path('docs/idcard-print-scale.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
