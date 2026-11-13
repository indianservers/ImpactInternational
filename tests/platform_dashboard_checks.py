"""Repeatable role/scope/dashboard checks. Fixtures are restricted to the isolated QA DB."""
import datetime,json,os,pathlib,sys,uuid,time
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095'
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_dues_upgrade_')
audit=json.loads(pathlib.Path('docs/qa/platform-role-audit.json').read_text())
def uid(key):return str(uuid.uuid5(uuid.NAMESPACE_URL,'impact-dashboard-qa/'+key))
def scalar(q):return sql(q)
def org(code):return scalar(f'''SELECT "Id" FROM "Orgs" WHERE "Code"='{code}' ''')
tenant=scalar('SELECT "Id" FROM "Tenants" LIMIT 1')
root=scalar('SELECT "Id" FROM "Orgs" WHERE "ParentId" IS NULL LIMIT 1')
club=org('DEMO-R01-D01-Z01-C01');other=org('DEMO-R02-D01-Z01-C01')
def make(name,role,scope):
 email='qa.dashboard.'+name+'@example.test';user=uid(name);assignment=uid(name+'-assignment')
 sql(f'''INSERT INTO "Users" ("Id","Email","PasswordHash","Verified","PlatformAdmin","FailedAttempts") SELECT '{user}','{email}',"PasswordHash",true,false,0 FROM "Users" WHERE "Email"='admin@example.test' ON CONFLICT ("Id") DO NOTHING;
 INSERT INTO "UserProfiles" ("Id","Name","Phone","Locale","TimeZone","Theme") VALUES ('{user}','QA {name}','','en','Asia/Kolkata','light') ON CONFLICT ("Id") DO NOTHING;
 INSERT INTO "Assignments" ("Id","UserId","TenantId","RoleId","OrgId") SELECT '{assignment}','{user}','{tenant}',"Id",'{scope}' FROM "Roles" WHERE "Name"='{role}' AND "TenantId"='{tenant}' ON CONFLICT ("Id") DO NOTHING;''')
 return dict(email=email,user=user,assignment=assignment,role=role,scope=scope)
fixtures=[]
for r in audit['roles']:
 role=r['name'];name=role.lower().replace(' / ','-').replace(' ','-')
 scope=root;kind='organization'
 for prefix,code,k in [('National','DEMO-NAT-01','national'),('Regional','DEMO-R01','regional'),('District','DEMO-R01-D01','district'),('Zone','DEMO-R01-D01-Z01','zonal'),('Zonal','DEMO-R01-D01-Z01','zonal')]:
  if role.startswith(prefix):scope=org(code);kind=k
 if role.startswith('Club') or role=='Guest / Applicant':scope=club;kind='club'
 if 'members.view' not in r['permissions']:kind='member'
 if role=='Club Secretary':kind='club-operations'
 if role=='Club Treasurer':kind='finance'
 fixture=make(name,role,scope);fixture['kind']=kind;fixtures.append(fixture)
results=[]
def check(name,condition,detail=None):
 results.append(dict(scenario=name,passed=bool(condition),detail=detail))
 if not condition:print('FAIL',name,str(detail)[:350],flush=True)
def status(c,name,path,expected,method='GET',body=None):
 s,d=c.req(path,method,body);check(name,s in ([expected] if isinstance(expected,int) else expected),dict(status=s,response=d if s>=500 else None));return s,d
admin=Client();status(admin,'Platform administrator password login','/auth/login',200,'POST',dict(email='saisatish@indianservers.com',password=__import__('support').PASSWORD))
_,platform=status(admin,'Platform intelligence authorized','/dashboard?view=platform',200)
expectedClubs=int(scalar('SELECT count(*) FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."IsClub"'))
expectedMembers=int(scalar('SELECT count(DISTINCT "PersonId") FROM "Memberships"'))
values={m['id']:m for m in platform['metrics']}
check('Club KPI reconciles to SQL',values['clubs']['value']==expectedClubs,expectedClubs)
check('Registered member KPI reconciles to SQL',values['registered']['value']==expectedMembers,expectedMembers)
check('All ten KPI comparisons explicit',len(values)==10 and all(m['comparison'] and 'previous' in m for m in values.values()))
check('Zero baseline has no fabricated percentage',all(m['deltaPercent'] is None for m in values.values() if m['previous']==0))
check('Region distribution reconciles',sum(x['clubs'] for x in platform['regions'])==expectedClubs)
check('District distribution reconciles',sum(x['clubs'] for x in platform['districts'])==expectedClubs)
check('Status distribution reconciles',sum(x['count'] for x in platform['clubStatus'])==expectedClubs)
check('Cumulative membership chart reconciles',platform['trend'][-1]['members']==expectedMembers)
check('Officer vacancies and coverage consistent',platform['health']['mandatoryPositions']-platform['health']['occupiedPositions']==next(p['count'] for p in platform['priorities'] if p['id']=='vacancies'))
for period in ['7d','30d','90d','6m','12m']:
 _,d=status(admin,'Date filter '+period,'/dashboard?view=platform&range='+period,200);check('Date filter preserves current snapshot '+period,next(x['value'] for x in d['metrics'] if x['id']=='clubs')==expectedClubs)
today=datetime.date.today();fromDate=(today-datetime.timedelta(days=14)).isoformat()
status(admin,'Custom date range','/dashboard?view=platform&range=custom&from='+fromDate+'&to='+today.isoformat(),200)
for suffix in ['range=nope','range=custom&from=1900-01-01','range=custom&from=1800-01-01','range=custom&to=2099-01-01','range=custom&from=2026-11-13&to=2026-01-01','format=pdf']:
 status(admin,'Reject invalid filter '+suffix,'/dashboard?view=platform&'+suffix,400)
s,body,_=admin.request('/dashboard?view=platform&format=csv');check('Platform CSV export authorized',s==200 and b'Connected clubs' in body)
check('Privileged export logged',int(scalar('''SELECT count(*) FROM "Audits" WHERE "Action"='platform.dashboard.export' '''))>0)
clients={}
for f in fixtures:
 c=Client();clients[f['role']]=c
 status(c,f['role']+': login','/auth/login',200,'POST',dict(email=f['email'],password=__import__('support').PASSWORD))
 _,d=status(c,f['role']+': dashboard route','/dashboard?view=route',200)
 check(f['role']+': exact scoped experience',d.get('kind')==f['kind'] and d.get('scope',{}).get('id')==f['scope'] and d.get('platformAdmin') is False,d)
 status(c,f['role']+': platform direct API denied','/dashboard?view=platform',403)
 status(c,f['role']+': platform export denied','/dashboard?view=platform&format=csv',403)
 status(c,f['role']+': export without platform view denied','/dashboard?format=csv',403)
 status(c,f['role']+': forged role ID denied','/dashboard?roleId='+uid('forged-role'),403)
 _,d=status(c,f['role']+': scoped dashboard','/dashboard?view=scoped',200)
 role=next(r for r in audit['roles'] if r['name']==f['role'])
 if 'audit.view' not in role['permissions']:check(f['role']+': audit data not leaked',d.get('recent')==[])
 if 'members.view' not in role['permissions']:check(f['role']+': aggregate data not leaked',d.get('organizations') is None and d.get('clubs') is None and 'activeMembers' not in d)
 else:
  expected=int(scalar(f'''WITH RECURSIVE nodes AS (SELECT "Id" FROM "Orgs" WHERE "Id"='{f['scope']}' UNION ALL SELECT o."Id" FROM "Orgs" o JOIN nodes n ON o."ParentId"=n."Id") SELECT count(*) FROM nodes'''))
  check(f['role']+': subtree count reconciles',d.get('organizations')==expected,dict(expected=expected,actual=d.get('organizations')))
 if f['scope']!=root:
  status(c,f['role']+': ancestor parameter denied','/dashboard?orgId='+root,404)
  if f['kind'] not in ['national']:status(c,f['role']+': foreign branch record denied','/organizations/'+other,404)
  status(c,f['role']+': another user workspace denied','/workspaces/'+uid('association-super-admin-assignment')+'/activate',[403,404],'POST',{})
for role in ['Club Member','Guest / Applicant']:
 c=clients[role]
 for path in ['/roles','/assignments','/audit-logs','/administration/dashboard']:
  status(c,role+': protected module '+path,path,403)
 c.req('/users/profile','PUT',dict(name='QA '+role,phone='',locale='en',timeZone='Asia/Kolkata',theme='light',platformAdmin=True,roleId=uid('tampered')))
 check(role+': profile cannot elevate authority',c.req('/auth/me')[1]['platformAdmin'] is False)
member=clients['Club Member']
for role,code in [('District Administrator','DEMO-R01-D02'),('District President','DEMO-R01-D02'),('Regional Administrator','DEMO-R02'),('Regional President','DEMO-R02')]:
 c=clients[role];foreign=org(code)
 status(c,role+': sibling unit detail denied','/organizations/'+foreign,404)
 status(c,role+': sibling unit analytics parameter denied','/dashboard?orgId='+foreign,404)
status(member,'Member accounting report export denied','/finance/dues-center/reports/pending?format=csv',403)
s,_,_=member.request('/users/profile','PUT',json.dumps(dict(name='QA',phone='')).encode(),{'Content-Type':'application/json','X-CSRF':''});check('Missing CSRF mutation rejected',s==400,s)
status(Client(),'Anonymous platform API denied','/dashboard?view=platform',401)
# Role delegation regression: a limited delegator must not assign a stronger role.
delegatorRole=uid('limited-role')
sql(f'''INSERT INTO "Roles" ("Id","TenantId","Name") VALUES ('{delegatorRole}','{tenant}','QA Limited Delegator') ON CONFLICT ("Id") DO NOTHING;
 INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") VALUES ('{uid('limited-view')}','{delegatorRole}','organization.view'),('{uid('limited-assign')}','{delegatorRole}','roles.assign') ON CONFLICT DO NOTHING;''')
f=make('limited-delegator','QA Limited Delegator',club);limited=Client();limited.login(f['email'])
strong=scalar('''SELECT "Id" FROM "Roles" WHERE "Name"='Association Super Admin' ''')
status(limited,'Loaded grants prevent stronger role delegation','/assignments',403,'POST',dict(email=fixtures[0]['email'],roleId=strong,orgId=club))
status(limited,'Delegation outside own subtree denied','/assignments',403,'POST',dict(email=fixtures[0]['email'],roleId=delegatorRole,orgId=other))
# Multi-workspace switching must replace scope rather than union it.
switch=next(f for f in fixtures if f['role']=='Regional Administrator');second=uid('regional-second')
sql(f'''INSERT INTO "Assignments" ("Id","UserId","TenantId","RoleId","OrgId") SELECT '{second}','{switch['user']}','{tenant}',"Id",'{org('DEMO-R02')}' FROM "Roles" WHERE "Name"='Regional Administrator' ON CONFLICT ("Id") DO NOTHING''')
c=clients['Regional Administrator'];status(c,'Switch to owned alternate region','/workspaces/'+second+'/activate',204,'POST',{})
check('Role switch changes active scope',c.req('/dashboard?view=route')[1]['scope']['id']==org('DEMO-R02'))
status(c,'Old region inaccessible after switching','/organizations/'+switch['scope'],404)
status(c,'Switch never grants platform authority','/dashboard?view=platform',403)
c.req('/workspaces/'+switch['assignment']+'/activate','POST',{})
# Committee responsibility is a dated relationship, separate from organizational roles.
committee=uid('committee');chairRole=uid('committee-chair-role');participantRole=uid('committee-member-role')
sql(f'''INSERT INTO "Committees" ("Id","TenantId","OrgId","Name","Code","Description","Purpose","Category","Notes","StartsAt","EndsAt","Status","Version") VALUES ('{committee}','{tenant}','{club}','QA Dashboard Committee','QA-DASHBOARD','QA only','Access isolation checks','Custom','QA only',now()-interval '1 day',now()+interval '1 year','Active',1) ON CONFLICT ("Id") DO NOTHING;
 INSERT INTO "CommitteeRoles" ("Id","TenantId","Name","CanManage","Active") VALUES ('{chairRole}','{tenant}','QA Dashboard Chair',true,true),('{participantRole}','{tenant}','QA Dashboard Participant',false,true) ON CONFLICT ("Id") DO UPDATE SET "Active"=true;''')
for name,committeeRole,manage in [('committee-chair',chairRole,True),('committee-member',participantRole,False)]:
 f=make(name,'Club Member',club)
 person=scalar(f'''SELECT "Id" FROM "Persons" WHERE "UserId"='{f['user']}' LIMIT 1''')
 if not person:
  person=scalar('''SELECT "Id" FROM "Persons" WHERE "UserId" IS NULL ORDER BY "Id" LIMIT 1''')
  sql(f'''UPDATE "Persons" SET "UserId"='{f['user']}' WHERE "Id"='{person}' ''')
 sql(f'''INSERT INTO "CommitteeMemberships" ("Id","TenantId","CommitteeId","PersonId","RoleId","Responsibilities","StartsAt","EndsAt","Status","Version") VALUES ('{uid(name+'-membership')}','{tenant}','{committee}','{person}','{committeeRole}','QA',now()-interval '1 day',now()+interval '1 year','Active',1) ON CONFLICT ("Id") DO NOTHING''')
 c=Client();check(name+': login',c.login(f['email'])[0]==200)
 _,d=status(c,name+': committee dashboard','/dashboard?view=route',200)
 check(name+': own relationship only',d.get('kind')=='committee' and len(d.get('committees',[]))==1 and d['committees'][0]['canManage']==manage)
 _,d=status(c,name+': committee detail','/committees/'+committee,200);check(name+': exact management authority',d.get('canManage')==manage)
 status(c,name+': no platform authority','/dashboard?view=platform',403)
 sql(f'''UPDATE "CommitteeRoles" SET "Active"=false WHERE "Id"='{committeeRole}' ''')
 status(c,name+': inactive role blocks detail','/committees/'+committee,404)
 check(name+': inactive role omitted from mine',c.req('/committees?mine=true')[1]['total']==0)
 check(name+': inactive role removed from dashboard',c.req('/dashboard?view=route')[1]['kind']=='member')
 sql(f'''UPDATE "CommitteeRoles" SET "Active"=true WHERE "Id"='{committeeRole}' ''')
# Revocation of an assignment is effective on the next API request.
f=make('revoked-workspace','Club Member',club);revoked=Client();revoked.login(f['email'])
sql(f'''DELETE FROM "Assignments" WHERE "Id"='{f['assignment']}' ''')
status(revoked,'Revoked assignment blocks dashboard','/dashboard?view=route',[403,404])
# Session expiry is checked at the server on every call.
temporary=Client();temporary.login(fixtures[0]['email'])
sql(f'''UPDATE "Sessions" SET "ExpiresAt"=now()-interval '1 minute' WHERE "UserId"='{fixtures[0]['user']}' ''')
status(temporary,'Expired session cannot retrieve analytics','/dashboard?view=platform',401)
output=dict(database=os.environ['CLUBS_TEST_DATABASE'],runAt=datetime.datetime.now(datetime.timezone.utc).isoformat(),total=len(results),passed=sum(r['passed'] for r in results),failed=sum(not r['passed'] for r in results),roleAccounts=fixtures,scenarios=results)
pathlib.Path('docs/qa/platform-dashboard-results.json').write_text(json.dumps(output,indent=2))
print(json.dumps({k:output[k] for k in ['total','passed','failed']}))
sys.exit(1 if output['failed'] else 0)
