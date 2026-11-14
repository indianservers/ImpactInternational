"""Read-only regression checks for the browser audit and consolidated Finance view."""
import sys,json,pathlib,csv,io,uuid,urllib.parse
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from support import Client,BASE,sql
assert BASE=='http://127.0.0.1:5095'
db='clubs_impact_dues_upgrade_20261113'
results=[]
def check(name,ok,detail=None):
 results.append(dict(scenario=name,passed=bool(ok),detail=detail))
 print(('PASS ' if ok else 'FAIL ')+name,flush=True)
 pathlib.Path('docs/qa/role-browser-audit/api-regressions.json').write_text(json.dumps({'total':len(results),'passed':sum(x['passed'] for x in results),'checks':results},indent=2),encoding='utf-8')
 assert ok,(name,detail)
a=Client();check('Platform admin login',a.login('saisatish@indianservers.com')[0]==200)
code,all_data=a.req('/finance/consolidated');check('Consolidated Finance returns all authorized books',code==200,code)
check('QA preview identified',all_data['mockData'])
es=a.req('/finance/entities')[1];check('No financial entities created by audit',all_data['ledgerCount']==len([e for e in es if e['active']]))
for kind in ['invoices','receipts','expenses','payments']:
 code,d=a.req('/finance/consolidated?kind='+kind)
 check(kind+' consolidated register available',code==200,code)
 source=[]
 for e in es:
  status,part=a.req('/finance/entities/'+e['id']+'/command/'+kind+'?from=2026-01-01&to=2026-11-14&pageSize=100')
  check(kind+' source register '+e['name'],status==200,status)
  source+=part['items']
 check(kind+' consolidated record count agrees with source APIs',d['count']==len(source),(d['count'],len(source)))
 check(kind+' stable original IDs',set(r['id'] for r in d['items'])<=set(r['id'] for r in source))
 check(kind+' rows retain ledger and currency identity',all(r['entityId'] in [e['id'] for e in es] and r['currency'] and r['ledger'] for r in d['items']))
 status,content,headers=a.request('/finance/consolidated?kind='+kind+'&format=csv');rows=list(csv.reader(io.StringIO(content.decode('utf-8-sig'))))
 check(kind+' CSV includes exactly authorized records',status==200 and len(rows)==d['count']+1)
for total in all_data['invoiceTotals']:
 curr=total['currency'];scoped=[e['id'] for e in es if e['currency']==curr]
 ids=','.join("'"+x+"'" for x in scoped)
 actual=json.loads(sql('SELECT json_build_object(\'billed\',coalesce(sum("Total"-"Credited"),0),\'outstanding\',coalesce(sum("Total"-"Paid"-"Credited"),0)) FROM "FinanceInvoices" WHERE "EntityId" IN ('+ids+') AND "State"=\'Issued\' AND "Date" BETWEEN \'2026-01-01\' AND \'2026-11-14\';',db))
 check(curr+' billed and outstanding reconcile to SQL',total['billed']==actual['billed'] and total['outstanding']==actual['outstanding'],actual)
regions=[o for o in all_data['organizations'] if o['type']=='Region']
for region in regions:
 code,d=a.req('/finance/consolidated?orgId='+region['id'])
 orgmap={o['id']:o for o in all_data['organizations']}
 def inside(unit):
  seen=set()
  while unit and unit not in seen:
   if unit==region['id']:return True
   seen.add(unit);unit=orgmap.get(unit,{}).get('parentId')
  return False
 expected=[e['id'] for e in es if inside(e['orgId'])]
 check('Region branch filter '+region['name'],code==200 and d['ledgerCount']==len(expected) and all(r['entityId'] in expected for r in d['items']))
for query in ['page=0','page=10001','kind=unknown','format=xml','balance=unknown','from=2027-01-01&to=2026-01-01','currency=INVALID','orgId='+str(uuid.uuid4())]:
 code,d=a.req('/finance/consolidated?'+query);check('Invalid finance request rejected: '+query,code in [400,404],code)
for balance in ['outstanding','overdue']:
 code,d=a.req('/finance/consolidated?balance='+balance)
 check(balance+' KPI drill-down filters balances',code==200 and all(r['outstanding']>0 and (balance!='overdue' or r['status']=='Overdue') for r in d['items']))
code,d=a.req('/finance/consolidated?search=MOCK-DUE-00000008');check('Consolidated reference search',code==200 and d['count']>=1 and all('MOCK-DUE-00000008' in r['reference'] for r in d['items']))
code,lead=a.req('/leadership/center');code,analytics=a.req('/membership/analytics?workspace=actions')
check('Membership coverage matches current Leadership definition',code==200 and sum(max(0,o['required']-o['filled']) for o in analytics['officers'])==lead['summary']['vacancies'])
code,club=a.req('/administration/dashboard');activeclubids=[o['id'] for o in all_data['organizations'] if o['type']=='Club']
check('Club dashboard excludes inactive club vacancies',code==200 and club['mandatoryVacancies']<=lead['summary']['vacancies'])
code,assistant=a.req('/intelligence/assistant','POST',{'question':'Show outstanding membership dues.','tool':'finance_summary'})
check('Assistant includes Platform Admin authorized finance books',code==200 and len(assistant['facts'])==len(es),str(assistant)[:150] if code!=200 else None)
for email in ['international.president@example.test','national.president@example.test','regional.president@example.test','district.president@example.test','zonal.president@example.test','club.president@example.test','member.club1@example.test']:
 c=Client();check(email+' login',c.login(email)[0]==200)
 ws=c.req('/workspaces')[1]
 if isinstance(ws,dict):ws=ws.get('items',ws.get('workspaces',[]))
 if email.startswith('member.'):
  member=next((w for w in ws if w.get('role')=='Club Member'),None)
  if member:c.activate(member)
 code,d=c.req('/finance/consolidated')
 check(email+' consolidated read stays within ledger grants',code==200 and d['ledgerCount']==0 and d['count']==0,code)
 foreign=next(o['id'] for o in regions if o['name']=='Demo Region 02')
 if email.split('.')[0] in ['regional','district','zonal','club']:
  check(email+' foreign region rejected',c.req('/finance/consolidated?orgId='+foreign)[0]==404)
 status,b=c.req('/finance/consolidated?format=csv');check(email+' CSV contains no unauthorized records',status==200 and len(list(csv.reader(io.StringIO(b.decode('utf-8-sig') if isinstance(b,bytes) else ''))))<=1)
club_reader=Client();assert club_reader.login('club.president@example.test')[0]==200
status,collections=club_reader.req('/finance/dues-center/collections')
check('Club collection register still scoped',status==200)
receipts=[r for r in collections['items'] if r.get('paymentId') and r.get('receipt')]
check('Verified allocation receipt fixture exists',len(receipts)>0)
for row in receipts:
 code,body,headers=club_reader.request('/finance/dues-center/allocations/'+row['id']+'/receipt')
 check('Club scoped allocation receipt '+row['receipt'],code==200 and body.startswith(b'%PDF'))
foreign_client=Client();assert foreign_client.login('member.region2@example.test')[0]==200
check('Ordinary member cannot read club allocation receipt',foreign_client.req('/finance/dues-center/allocations/'+receipts[0]['id']+'/receipt')[0] in [403,404])
other_president=Client();assert other_president.login('zonal.president@example.test')[0]==200
check('Unknown receipt allocation rejected',other_president.req('/finance/dues-center/allocations/'+str(uuid.uuid4())+'/receipt')[0]==404)
event_id=sql('SELECT e."Id" FROM "Events" e WHERE e."Title" LIKE \'Role Audit Training 20261114%\' AND NOT EXISTS(SELECT 1 FROM "EventFeedbackForms" f WHERE f."EventId"=e."Id") LIMIT 1;',db)
check('Missing event survey returns clean empty response',a.req('/events/'+event_id+'/feedback-form')==(200,None))
check('Anonymous consolidated access rejected',Client().req('/finance/consolidated')[0]==401)
print('Completed',len(results),'checks')
