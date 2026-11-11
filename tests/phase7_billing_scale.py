"""Explicit real API billing throughput test on the isolated scale database."""
import os,sys,pathlib,json,subprocess,urllib.request,time,datetime,uuid
assert '--run' in sys.argv,'Use --run to create a new fictional billing batch in clubs_governance_scale'
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5087'
from support import Client,sql
DB='clubs_governance_scale';env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':f'Host=127.0.0.1;Port=55432;Database={DB};Username=clubs','Logging__LogLevel__Default':'Warning'};log=open('.infra/phase7-billing-api.log','w');process=subprocess.Popen(['dotnet','server/bin/Debug/net10.0/Clubs.Api.dll','--urls','http://127.0.0.1:5087'],env=env,stdout=log,stderr=log)
def good(c,p,m='GET',d=None):code,b=c.req(p,m,d);assert code in (200,204),(p,code,b);return b
try:
 for _ in range(80):
  try:urllib.request.urlopen('http://127.0.0.1:5087/health',timeout=1);break
  except:time.sleep(.2)
 c=Client();assert c.login('admin@example.test')[0]==200;w=next(w for w in good(c,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(w);e=next(e for e in good(c,'/finance/entities') if e['name']=='Finance benchmark fund');p='/finance/entities/'+e['id'];club=sql('SELECT "ClubId" FROM "Memberships" GROUP BY "ClubId" ORDER BY count(*) DESC LIMIT 1',database=DB);expected=int(sql(f"SELECT count(*) FROM \"Memberships\" WHERE \"ClubId\"='{club}' AND \"Status\"='Active' AND (\"EndsAt\" IS NULL OR \"EndsAt\">now())",database=DB));assert expected>=1000,expected
 good(c,p+'/organizations','POST',{'orgId':club,'authority':'Fictional scale billing test authorization only'});year=datetime.date.today().year;plan=good(c,p+'/dues-plans','POST',{'name':'API billing throughput '+uuid.uuid4().hex[:8],'billingOrgId':club,'purpose':'ClubDues','effectiveFrom':f'{year}-01-01','effectiveTo':f'{year}-12-31','components':[{'description':'Fictional benchmark annual fee','amount':'10'}]});start=time.perf_counter();job=good(c,'/finance/dues-plans/'+plan['id']+'/batches','POST',{'startsOn':f'{year}-01-01','endsOn':f'{year+1}-01-01'});first=None
 for _ in range(600):
  state=next(x for x in good(c,p+'/jobs') if x['id']==job['id'])
  if state['processed'] and first is None:first=time.perf_counter()-start
  if state['status'] in ('Completed','Failed'):break
  time.sleep(.25)
 elapsed=time.perf_counter()-start;assert state['status']=='Completed' and state['processed']==expected,state
 count=int(sql(f"SELECT count(*) FROM \"FinanceInvoices\" WHERE \"PlanId\"='{plan['id']}'",database=DB));assert count==expected
 result={'members':expected,'issuedInvoices':count,'jobState':state['status'],'elapsed_seconds_including_idle_pickup':round(elapsed,3),'first_progress_seconds':round(first or 0,3),'conditions':'Actual queued API assessment/numbering/journal posting with EF, PostgreSQL safeguards and 50-member durable chunks. Local shared Windows/PostgreSQL 17.6, .NET 10 Debug; no live collection/provider. The separate 100k fixture is SQL generated.','repeatSafe':good(c,'/finance/dues-plans/'+plan['id']+'/batches','POST',{'startsOn':f'{year}-01-01','endsOn':f'{year+1}-01-01'})['id']==job['id']};pathlib.Path('test-results/phase7-billing-scale.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2),flush=True)
finally:process.terminate();process.wait(timeout=20);log.close()
