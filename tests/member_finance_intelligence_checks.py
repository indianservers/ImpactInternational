"""Member roster completeness, financial reconciliation and scope regressions."""
import json, pathlib, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from support import Client, BASE
assert BASE == 'http://127.0.0.1:5095'
checks=[]
def check(name, passed):
    checks.append({'name':name,'passed':bool(passed)})
    assert passed,name
a=Client();check('Admin login',a.login('saisatish@indianservers.com')[0]==200)
orgs=a.req('/finance/dues-center/options')[1]['organizations']
club=next(o for o in orgs if o['name']=='Demo Region 01 District 01 Zone 01 Club 01')
foreign=next(o for o in orgs if o['name']=='Demo Region 02 District 01 Zone 01 Club 01')
url='/finance/dues-center/member-intelligence?orgId='+club['id']
code,d=a.req(url);check('Member intelligence available',code==200)
c=d['counts'];s=d['summary'];items=d['items']
check('Payment categories partition all memberships',c['total']==c['paid']+c['partial']+c['unpaid']+c['noDues'])
check('Members without invoices included',c['noDues']>0)
check('One row per membership',len(items)==len(set(r['id'] for r in items)))
check('Rows remain in selected club',all(r['club']==club['name'] for r in items))
check('No-dues members are not treated as unpaid',all(r['invoices']==0 and r['pending']==0 for r in items if r['status']=='No dues'))
_,report=a.req('/finance/dues-center/reports/club?orgId='+club['id'])
summary=report['items'][0]
for key in ['billed','collected','pending','overdue']:
    check(key+' reconciles to club dues report',abs(s[key]-summary[key])<0.001)
for status in ['Paid','Partial','Unpaid','No dues','Overdue','Verification','HasPaid','Pending']:
    code,r=a.req(url+'&status='+status.replace(' ','%20'))
    expected=[x for x in items if (x['overdue']>0 if status=='Overdue' else x['verification']>0 if status=='Verification' else x['collected']>0 if status=='HasPaid' else x['pending']>0 if status=='Pending' else x['status']==status)]
    check(status+' chart filter returns matching members',code==200 and r['total']==len(expected))
    check(status+' filtering preserves full-scope chart counts',r['counts']==c)
_,usd=a.req(url+'&currency=USD');check('No currency mixing',usd['currency']=='USD' and usd['summary']['collected']==0 and usd['counts']['noDues']==usd['counts']['total'])
check('Invalid member status rejected',a.req(url+'&status=Invalid')[0]==400)
p=Client();check('Club president login',p.login('club.president@example.test')[0]==200)
check('President can read assigned club',p.req(url)[0]==200)
check('Foreign-club roster blocked',p.req('/finance/dues-center/member-intelligence?orgId='+foreign['id'])[0]==404)
m=Client();check('Member login',m.login('member.club1@example.test')[0]==200)
check('Ordinary member cannot read finance roster',m.req(url)[0]==403)
out=pathlib.Path('docs/qa/member-finance-intelligence');out.mkdir(parents=True,exist_ok=True)
(out/'api-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(f'Passed {len(checks)} checks')
