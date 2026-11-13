"""Read-model integration checks against an explicitly isolated database copy."""
import io,json,pathlib,sys,uuid,zipfile,xml.etree.ElementTree as ET
from urllib.parse import urlencode
from support import Client,sql,BASE
import os
assert os.environ.get('CLUBS_TEST_DATABASE','').startswith(('clubs_finance_command_','clubs_finance_workflows_'))
assert BASE in ('http://127.0.0.1:5199','http://127.0.0.1:5200')
results=[]
def check(name,ok):
 results.append(dict(test=name,passed=bool(ok)))
 if not ok: raise AssertionError(name)
def good(c,p):
 status,data=c.req(p);check('GET '+p,status==200)
 return data
try:
 c=Client();check('Admin login',c.login('admin@example.test')[0]==200)
 if BASE.endswith('5200'):
  workspace=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(workspace)
 entities=good(c,'/finance/entities');e=next((e for e in entities if 'C01' in e['name']),entities[0]);base='/finance/entities/'+e['id']
 pages=['overview','transactions','receipts','invoices','payments','expenses','reconciliation','budgets','audit','ledger','gateway','scheduled']
 for page in pages:
  p=base+'/command/'+page
  initial=good(c,p);check(page+' page bounded',len(initial['items'])<=25)
  check(page+' entity isolation label',initial['entity']==e['name'])
  for sort in ['date','amount','party','reference','outstanding']:
   for direction in ['asc','desc']:
    d=good(c,p+'?'+urlencode(dict(sort=sort,direction=direction,pageSize=2)))
    vals=[r[sort] for r in d['items']]
    check(page+' '+sort+' '+direction+' ordered',vals==sorted(vals,reverse=direction=='desc'))
    check(page+' pagination bounded '+sort+direction,len(d['items'])<=2)
  d=good(c,p+'?page=999&pageSize=1');check(page+' exhausted page empty',d['items']==[])
  d=good(c,p+'?search='+uuid.uuid4().hex);check(page+' search absent',d['count']==0)
  for format in ['csv','xlsx','pdf']:
   status,body,headers=c.request(p+'?format='+format);check(page+' '+format+' export',status==200)
   if format=='xlsx':
    with zipfile.ZipFile(io.BytesIO(body)) as z:
     ET.fromstring(z.read('xl/worksheets/sheet1.xml'));check(page+' workbook has manifest','[Content_Types].xml' in z.namelist())
   if format=='pdf':check(page+' valid PDF signature',body.startswith(b'%PDF'))
 for entity in entities[:3]:
  p='/finance/entities/'+entity['id'];m=good(c,p+'/command-metrics');eid=entity['id']
  billed=float(sql(f"SELECT coalesce(sum(\"Total\"),0) FROM \"FinanceInvoices\" WHERE \"EntityId\"='{eid}' AND \"State\"='Issued'"))
  paid=float(sql(f"SELECT coalesce(sum(\"Paid\"),0) FROM \"FinanceInvoices\" WHERE \"EntityId\"='{eid}' AND \"State\"='Issued'"))
  check(entity['name']+' generated reconciles to SQL',m['generated']==billed)
  check(entity['name']+' allocated reconciles to SQL',m['allocated']==paid)
  check(entity['name']+' invoice equation',round(m['generated']-m['allocated']-m['credits']-m['outstanding'],2)==0)
  check(entity['name']+' source totals',round(sum(r['amount'] for r in m['sources'])-m['collections'],2)==0)
  check(entity['name']+' ageing totals',round(sum(r['amount'] for r in m['ageing'])-m['outstanding'],2)==0)
 for query in ['page=0','pageSize=101','sort=sql','direction=invalid','quick=invalid','format=invalid','columns=private','from=2026-12-31&to=2026-01-01']:
  check('Invalid filter rejected '+query,c.req(base+'/command/transactions?'+query)[0]==400)
 check('Unknown workspace rejected',c.req(base+'/command/no-such-page')[0]==404)
 check('Unknown entity rejected',c.req('/finance/entities/'+str(uuid.uuid4())+'/command/invoices')[0]==404)
 anonymous=Client()
 for page in pages:check('Anonymous denied '+page,anonymous.req(base+'/command/'+page)[0] in (401,403))
 member=Client();check('Member login',member.login('member.club1@example.test' if BASE.endswith('5199') else 'member@example.test')[0]==200)
 for page in pages:check('Member cannot read entity '+page,member.req(base+'/command/'+page)[0] in (401,403,404))
 for f in ['csv','xlsx','pdf']:check('Member export denied '+f,member.req(base+'/command/invoices?format='+f)[0] in (401,403,404))
except Exception as exc:
 results.append(dict(test='Exception: '+str(exc),passed=False))
finally:
 pathlib.Path('docs/qa/finance-command-api-results.json' if BASE.endswith('5199') else 'docs/qa/finance-command-rich-api-results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
 print(json.dumps(dict(passed=sum(r['passed'] for r in results),failed=sum(not r['passed'] for r in results),total=len(results))))
 if any(not r['passed'] for r in results):sys.exit(1)
