"""Finance scale fixture/benchmark. Mutations require --fixture and isolated clubs_governance_scale."""
import os,sys,pathlib,subprocess,json,time,uuid,statistics,urllib.request,datetime
from decimal import Decimal
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5087'
from support import Client,sql
DB='clubs_governance_scale';fixture='--fixture' in sys.argv;year=datetime.date.today().year;today=datetime.date.today().isoformat();env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':f'Host=127.0.0.1;Port=55432;Database={DB};Username=clubs','Logging__LogLevel__Default':'Warning'}
assert int(sql('SELECT count(*) FROM "Persons"',database=DB))>=100000,'Requires existing isolated 100k person scale fixture'
with open('.infra/phase7-scale-migrate.log','w') as out:subprocess.run(['dotnet','server/bin/Debug/net10.0/Clubs.Api.dll','--migrate'],env=env,stdout=out,stderr=out,check=True)
log=open('.infra/phase7-scale-api.log','w');process=subprocess.Popen(['dotnet','server/bin/Debug/net10.0/Clubs.Api.dll','--urls','http://127.0.0.1:5087'],env=env,stdout=log,stderr=log)
def good(c,p,m='GET',d=None):status,b=c.req(p,m,d);assert status in (200,204),(p,status,b);return b
try:
 for _ in range(80):
  try:urllib.request.urlopen('http://127.0.0.1:5087/health',timeout=1);break
  except:time.sleep(.2)
 c=Client();assert c.login('admin@example.test')[0]==200;w=next(w for w in good(c,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(w);e=next((e for e in good(c,'/finance/entities') if e['name']=='Finance benchmark fund'),None)
 if e is None:
  assert fixture,'Run --fixture once to create isolated benchmark records'
  e=good(c,'/finance/entities','POST',{'orgId':w['orgId'],'name':'Finance benchmark fund','country':'US','currency':'USD','decimals':2,'collectionAuthority':'Fictional isolated scale benchmark only; no real funds'});good(c,'/finance/entities/'+e['id']+'/periods','POST',{'name':'Benchmark year','startsOn':f'{year}-01-01','endsOn':f'{year}-12-31'})
 p='/finance/entities/'+e['id'];entity=e['id'];tenant=e['tenantId'];org=e['orgId'];period=good(c,p+'/settings')['periods'][0]['id'];actor=sql("SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='admin@example.test'",database=DB)
 existing=int(sql(f"SELECT count(*) FROM \"FinanceInvoices\" WHERE \"EntityId\"='{entity}' AND \"IdempotencyKey\" LIKE 'load:%'",database=DB));fixture_seconds=None
 if fixture and existing<100000:
  begun=time.perf_counter()
  for lo in range(existing+1,100001,10000):
   hi=min(lo+9999,100000)
   query=f'''BEGIN; SET LOCAL statement_timeout='120s';
CREATE TEMP TABLE finance_load ON COMMIT DROP AS SELECT n,md5('{entity}:invoice:'||n)::uuid AS iid,md5('{entity}:journal:'||n)::uuid AS jid,md5('{entity}:line:'||n)::uuid AS lid FROM generate_series({lo},{hi}) n;
INSERT INTO "FinanceInvoices" ("Id","TenantId","OrgId","EntityId","Version","Number","Customer","CustomerAddress","IssuerSnapshot","Purpose","Date","DueOn","State","Currency","Subtotal","Tax","Total","Paid","Credited","IdempotencyKey","Notes","CreatorId","RequestHash") SELECT iid,'{tenant}','{org}','{entity}',1,'LOAD-'||lpad(n::text,8,'0'),'Fictional scale member '||n,'','Finance benchmark fund','ClubDues','{today}','{year}-12-31','Draft','USD',100,0,100,0,0,'load:'||n,'SQL-generated benchmark fixture; not payment verification evidence','{actor}','benchmark-fixture' FROM finance_load;
INSERT INTO "FinanceInvoiceLines" ("Id","TenantId","OrgId","EntityId","Version","InvoiceId","Description","Quantity","Rate","Discount","TaxPercent","Net","Tax","Total","AccountCode") SELECT lid,'{tenant}','{org}','{entity}',1,iid,'Annual dues benchmark',1,100,0,0,100,0,100,'4000' FROM finance_load;
INSERT INTO "FinanceJournals" ("Id","TenantId","OrgId","EntityId","Version","Date","PeriodId","Source","Narration","CreatorId","PostedAt") SELECT jid,'{tenant}','{org}','{entity}',1,'{today}','{period}','invoice:'||iid,'Benchmark issued invoice','{actor}',now() FROM finance_load;
INSERT INTO "FinanceJournalLines" ("Id","TenantId","OrgId","EntityId","Version","JournalId","AccountId","Debit","Credit") SELECT md5(l.jid::text||a."Code")::uuid,'{tenant}','{org}','{entity}',1,l.jid,a."Id",CASE WHEN a."Code"='1100' THEN 100 ELSE 0 END,CASE WHEN a."Code"='4000' THEN 100 ELSE 0 END FROM finance_load l CROSS JOIN "FinanceAccounts" a WHERE a."EntityId"='{entity}' AND a."Code" IN ('1100','4000');
UPDATE "FinanceInvoices" SET "State"='Issued' WHERE "EntityId"='{entity}' AND "Id" IN(SELECT iid FROM finance_load); COMMIT;'''
   sql(query,database=DB);print('Seeded',hi,'balanced benchmark invoices',flush=True)
  fixture_seconds=round(time.perf_counter()-begun,3)
 sql('ANALYZE "FinanceInvoices"; ANALYZE "FinanceJournalLines"; ANALYZE "FinanceJournals";',database=DB)
 measurements={}
 for name,url in [('invoice_page',p+'/invoices'),('trial_balance',p+'/reports/trial-balance'),('general_ledger',p+'/reports/general-ledger'),('dashboard',p+'/dashboard')]:
  good(c,url);times=[]
  for _ in range(30):t=time.perf_counter();good(c,url);times.append((time.perf_counter()-t)*1000)
  measurements[name]={'requests':30,'p50_ms':round(statistics.median(times),2),'p95_ms':round(sorted(times)[28],2),'errors':0}
 counts={table:int(sql(f'SELECT count(*) FROM "{table}"',database=DB)) for table in ['Persons','FinanceInvoices','FinanceJournals','FinanceJournalLines']}
 explain=sql(f'''EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) SELECT "AccountId",sum("Debit"-"Credit") FROM "FinanceJournalLines" WHERE "EntityId"='{entity}' GROUP BY "AccountId"''',database=DB)
 pathlib.Path('test-results/phase7-explain.json').write_text(explain,encoding='utf-8');result={'conditions':'Local shared Windows workstation, PostgreSQL 17.6 loopback, .NET 10 Debug, warm sequential HTTP reads, no production SLO claim','fixturePolicy':'SQL-generated balanced invoice/journal fixtures with database triggers enabled. API financial correctness tested separately. No live payment or bank connection.','counts':counts,'fixture_seconds':fixture_seconds,'measurements':measurements};pathlib.Path('test-results/phase7-scale.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
finally:process.terminate();process.wait(timeout=20);log.close()
