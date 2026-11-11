"""Read-only warm HTTP measurements on the existing 100k-person synthetic database."""
import os,time,json,pathlib,statistics
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5088'
from support import Client,sql
c=Client();assert c.login('admin@example.test')[0]==200
code,ws=c.req('/workspaces');w=next(x for x in ws if x['association'].startswith('Global') and x['role']=='Association Super Admin');assert c.activate(w)[0]==204
cases={}
for name,path in [('global_dashboard','/intelligence/dashboard'),('member_search','/intelligence/search?query=Maya&kind=members'),('task_search','/intelligence/search?query=Scale&kind=tasks'),('all_source_search','/intelligence/search?query=Scale')]:
 times=[]
 for n in range(11):
  start=time.perf_counter();code,b=c.req(path);elapsed=(time.perf_counter()-start)*1000;assert code==200,(name,code,b)
  if n:times.append(elapsed)
 cases[name]={'samples':10,'medianMs':round(statistics.median(times),2),'p95Ms':round(sorted(times)[-1],2),'responseBytes':len(json.dumps(b))}
counts={t:int(sql(f'SELECT count(*) FROM "{t}"','clubs_governance_scale')) for t in ['Persons','Memberships','FinanceInvoices','ClubMeetings','OperationTasks','ServiceProjects']}
report={'conditions':'Development loopback HTTP; warm sequential requests, 10 samples/case, conservative nearest-rank p95=max for n=10. Shared workstation; no concurrent-user or distributed SLO claim. Existing synthetic fixtures retained.','counts':counts,'cases':cases};pathlib.Path('docs/phase8-scale.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
