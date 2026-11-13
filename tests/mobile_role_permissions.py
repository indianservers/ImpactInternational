"""Check role permissions and scoped reads in a disposable mobile QA database."""
import os,json,pathlib
from support import Client,sql
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_mobile_e2e_')
fixtures=json.loads(pathlib.Path('.infra/mobile-role-fixtures.json').read_text());results=[]
checks=[('organization.view','/organizations?size=100'),('members.view','/members?search=Ananya'),('roles.view','/roles'),('audit.view','/audit-logs'),('committees.view','/committees?search='),('events.manage','/events?manage=true'),('operations.view','/meetings?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z'),('members.cards','/id-cards/templates')]
for f in fixtures:
 c=Client();code,_=c.login(f['email']);assert code==200,(f['role'],'login',code)
 ws=c.req('/workspaces')[1];assert len(ws)==1 and ws[0]['role']==f['role'];w=ws[0];assert c.activate(w)[0]==204
 for permission,path in checks:
  expected=200 if permission in w['permissions'] else 403
  status,body=c.req(path);ok=status==expected
  # Visible organization records must all remain in the selected role's subtree.
  if status==200 and path.startswith('/organizations'):
   actual={x['id'] for x in body['items']}
   scope=set(sql(f"""WITH RECURSIVE t AS (SELECT "Id" FROM "Orgs" WHERE "Id"='{f['orgId']}' UNION ALL SELECT o."Id" FROM "Orgs" o JOIN t ON o."ParentId"=t."Id") SELECT "Id" FROM t""").splitlines())
   ok=ok and actual.issubset(scope)
  results.append({'role':f['role'],'path':path,'expected':expected,'status':status,'passed':ok})
  if not ok:print('FAIL',results[-1],str(body)[:200])
 # Private membership/finance self-service should work without granting staff rights.
 for path in ['/membership/me','/my/finance','/committees?mine=true','/events']:
  status,body=c.req(path);results.append({'role':f['role'],'path':path,'status':status,'passed':status==200})
 # Accounts without person profiles must never inherit guest registrations.
 for private_path in ['/events?mine=true','/my/invitations','/my/registrations']:
  status,body=c.req(private_path)
  items=body if isinstance(body,list) else body.get('items',[])
  results.append({'role':f['role'],'path':private_path,'status':status,'passed':status==200 and len(items)==0})
 # Public discovery is intentionally available without event-administration privileges.
 status,body=c.req('/events')
 if not any(p in w['permissions'] for p in ['events.manage','events.reports']):
  ids=[e['id'] for e in body['items']]
  public=int(sql('SELECT count(*) FROM "Events" WHERE "Publication"=\'Published\' AND "Visibility"=\'Public\' AND "Id" IN ('+','.join("'"+x+"'" for x in ids)+')')) if ids else 0
  results.append({'role':f['role'],'check':'discovery only exposes published public events to role-only accounts','passed':public==len(ids)})
pathlib.Path('docs/mobile-role-permissions.json').write_text(json.dumps({'checks':len(results),'passed':sum(r['passed'] for r in results),'results':results},indent=2))
print('Role permission checks:',sum(r['passed'] for r in results),'/',len(results))
assert all(r['passed'] for r in results)
