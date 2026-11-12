"""Read-only screen-data sweep; creates only login sessions and selects workspaces.
Run with CLUBS_TEST_URL set to the API or Vite origin.
For Vite, set CLUBS_OPENAPI_URL to the backend /openapi/v1.json URL. Never calls business mutations.
"""
import sys,json,pathlib,urllib.request,urllib.parse,os,datetime
from support import Client,BASE
results=[];data={};c=Client()
assert c.login('admin@example.test')[0]==200
workspaces=c.req('/workspaces')[1]
w=next(w for w in workspaces if w['organization']=='International Headquarters' and w['role']=='Association Super Admin')
assert c.activate(w)[0]==204
spec=json.load(urllib.request.urlopen(os.environ.get('CLUBS_OPENAPI_URL', BASE+'/openapi/v1.json')))
def check(path,label=None,expected=(200,204)):
    if path in data:return data[path]
    code,body=c.req(path);data[path]=(code,body)
    row={'path':path,'status':code,'passed':code in expected}
    if code not in expected:row['detail']=str(body.get('detail',body.get('title','')) if isinstance(body,dict) else body)[:180]
    results.append(row)
    if not row['passed']:print('FAIL',code,path,row.get('detail',''),flush=True)
    return code,body
def rows(body):
    return body if isinstance(body,list) else body.get('items',[]) if isinstance(body,dict) else []
def first(path):
    code,body=check(path)
    return next((str(x['id']) for x in rows(body) if isinstance(x,dict) and x.get('id')),None) if code==200 else None
query={
 '/leadership/eligible':{'orgId':w['orgId']}, '/event-people':{'orgId':w['orgId']},
 '/meetings':{'from':'2026-11-01T00:00:00Z','to':'2026-12-01T00:00:00Z'},
 '/operations/certificate-sources':{'type':'Project'}, '/intelligence/search':{'query':'community'},
 '/intelligence/knowledge':{'query':'community'}, '/id-cards/qr-preview':{'value':'layout-check'},
}
skip={'/communications/stream','/communications/unsubscribe','/development/mail','/my/id-card/download','/intelligence/export'}
for path,methods in spec['paths'].items():
    if 'get' not in methods or '{' in path or not path.startswith('/api/v1/'):continue
    path=path[len('/api/v1'):]
    if path in skip:continue
    actual=path+('?' + urllib.parse.urlencode(query[path]) if path in query else '')
    check(actual)
# Explicit searches reproduce parameter-name collisions, including the initially failing empty search.
for path in ['/committees','/members','/clubs','/officer-appointments','/governance/documents','/governance/resolutions','/projects','/tasks','/documents','/volunteers','/id-cards/members']:
    for search in ['', 'Service', "O'Reilly", '%_']:
        check(path+'?'+urllib.parse.urlencode({'search':search,'page':1}))
for source in ['Membership','Project','Event','Leadership']:
    check('/operations/certificate-sources?'+urllib.parse.urlencode({'type':source,'search':'Service'}))
check('/committees?mine=true&search=')
# Open real records; no invented IDs or invalid-token success claims.
sources={'/members':first('/members'),'/clubs':first('/clubs'),'/committees':first('/committees'),
 '/officer-appointments':first('/officer-appointments'),'/governance/requests':first('/governance/requests'),
 '/leadership-handovers':first('/leadership-handovers'),'/membership/applications':first('/membership/applications'),
 '/membership/imports':first('/membership/imports'),'/projects':first('/projects'),'/tasks':first('/tasks'),
 '/meetings':first('/meetings?'+urllib.parse.urlencode(query['/meetings'])),
 '/events':first('/events'),'/communications/conversations':first('/communications/conversations'),
 '/communications/campaigns':first('/communications/campaigns'),'/communications/automations':first('/communications/automations'),
 '/id-cards/templates':first('/id-cards/templates'),'/id-cards/issuances':first('/id-cards/issuances'),
 '/id-cards/bulk-jobs':first('/id-cards/bulk-jobs'),'/intelligence/workflows':first('/intelligence/workflows')}
excluded=['/download','/photo','/pdf','/preview','/calendar','/card','/cards','/exports','/export']
for path,methods in spec['paths'].items():
    if 'get' not in methods or path.count('{')!=1 or '/{id}' not in path:continue
    path=path.removeprefix('/api/v1');prefix,suffix=path.split('/{id}')
    if prefix not in sources or not sources[prefix] or any(suffix.startswith(s) for s in excluded) or '/export' in suffix:continue
    check(prefix+'/'+sources[prefix]+suffix)
# Finance's tools require explicit book access; inspect every entity actually granted to this account.
entities=rows(data.get('/finance/entities',(0,[]))[1])
for entity in entities[:2]:
    eid=str(entity['id'])
    for path,methods in spec['paths'].items():
        if 'get' not in methods or '{entity}' not in path or path.count('{')!=1:continue
        check(path.removeprefix('/api/v1').replace('{entity}',eid))
    invoices=check('/finance/entities/'+eid+'/invoices')[1]
    invoice=next((r['id'] for r in rows(invoices) if r.get('id')),None)
    if invoice:check('/finance/invoices/'+str(invoice))
# Authorization/scoping regressions: restricted roles must not gain all-committee access.
for email,role in [('manager@example.test','Club Manager'),('member@example.test','Club Member')]:
    other=Client();assert other.login(email)[0]==200
    ow=next(x for x in other.req('/workspaces')[1] if x['role']==role)
    assert other.activate(ow)[0]==204
    for path in ['/committees?mine=false&search=','/committees?mine=true&search=']:
        code,body=other.req(path);expected=200 if 'mine=true' in path or 'committees.view' in ow['permissions'] else 403
        ok=code==expected
        if code==200 and 'mine=false' in path:
            visible=other.req('/organizations?size=100')[1]
            ids={x['id'] for x in rows(visible)}
            if ids:ok=ok and all(x['orgId'] in ids for x in rows(body))
        results.append({'role':role,'path':path,'status':code,'passed':ok,'expected':expected})
# A foreign/nonexistent requested scope must produce no unscoped results.
code,body=check('/committees?orgId=00000000-0000-0000-0000-000000000001&search=')
assert code==200 and body['total']==0
output=pathlib.Path(os.environ.get('CLUBS_SMOKE_REPORT','docs/page-errors-smoke.json'))
report={'base':BASE,'checked':len(results),'passed':sum(x['passed'] for x in results),'results':results,
 'notes':['Read-only business API calls; auth sessions/workspace selection only.','Binary downloads, streams, private token endpoints and third-party webhooks excluded.','Record screens checked when an existing accessible fixture was available.']}
output.write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Screen data checks:',report['passed'],'/',report['checked'],flush=True)
sys.exit(0 if all(x['passed'] for x in results) else 1)
