"""Real HTTP/PostgreSQL leadership regression and fixture tests; named QA database only."""
import datetime,json,os,pathlib,sys,time,uuid,urllib.parse
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE']=='clubs_impact_dues_upgrade_20261113'
checks=[]
def check(name,ok,detail=None):
 checks.append(dict(scenario=name,passed=bool(ok),detail=detail))
 if not ok:print('FAIL',name,str(detail)[:300],flush=True)
def call(c,path,method='GET',body=None,expected=200):
 status,data=c.req(path,method,body);check(method+' '+path[:95],status==expected,dict(status=status,error=data if status>=400 else None));return data
now=datetime.datetime.now(datetime.timezone.utc)
def at(days):return (now+datetime.timedelta(days=days)).isoformat()
admin=Client();check('Platform admin login',admin.login('saisatish@indianservers.com')[0]==200)
review=Client();check('Independent admin login',review.login('admin@example.test')[0]==200)
basePeople=int(sql('SELECT count(*) FROM "Persons"'))
cfg=call(admin,'/governance/options');structure=call(admin,'/structure/intelligence?depth=5');nodes=[]
def walk(n):
 nodes.append(n)
 for c in n['children']:walk(c)
walk(structure['root']);index={n['id']:n for n in nodes}
club=next(n for n in nodes if n['code']=='DEMO-R01-D01-Z01-C01');path=[];cursor=club
while True:
 path.insert(0,cursor)
 if not cursor['parentId']:break
 cursor=index[cursor['parentId']]
person=sql("SELECT p.\"Id\" FROM \"Persons\" p JOIN \"Users\" u ON u.\"Id\"=p.\"UserId\" WHERE u.\"Email\"='member.club1@example.test'")
fixturePath=pathlib.Path('.infra/leadership-center-fixtures.json');fixture=json.loads(fixturePath.read_text()) if fixturePath.exists() else {};fixture={k:v for k,v in fixture.items() if 'id' in v};positions={};appointments=[]
for unit in path:
 kind=unit['level'];code='LP-QA-'+kind.upper();position=next((p for p in cfg['positions'] if p['code']==code),None)
 if not position:position=call(admin,'/officer-positions','POST',dict(code=code,title='QA '+kind+' Service Officer',levelId=unit['levelId'],mandatory=True,maximumHolders=1,defaultMonths=12,requireActiveMembership=True,approvalRequired=False))
 positions[kind]=position
 key='active-'+kind
 if key not in fixture:
  fixture[key]=call(admin,'/officer-appointments','POST',dict(orgId=unit['id'],personId=person,positionId=position['id'],startsAt=at(-.01),endsAt=at(20 if kind=='Club' else 365),method='Appointed',notes='QA existing central person appointment'))
  fixturePath.write_text(json.dumps(fixture),encoding='utf-8')
 appointment=fixture[key];appointments.append(appointment['id']);detail=call(admin,'/officer-appointments/'+appointment['id']);check(kind+' existing person ID preserved',detail['appointment']['personId']==person);check(kind+' effective title-only appointment',detail['appointment']['status']=='Active');check(kind+' no automatic mapped access',detail['appointment']['grantRoleId'] is None)
check('Multiple appointments create no people',int(sql('SELECT count(*) FROM "Persons"'))==basePeople)
summary=call(admin,'/leadership/center');check('One person with multiple offices counted once',summary['summary']['activeLeaders']==int(sql('SELECT count(DISTINCT "PersonId") FROM "OfficerAppointments" WHERE "Status"=\'Active\'')))
check('At least six effective assignments',summary['summary']['activeAppointments']>=6)
profile=call(admin,'/leadership/people/'+person);check('Consolidated profile has all six offices',len([a for a in profile['appointments'] if a['current']])>=6);check('Profile reuses central person identity',profile['profile']['id']==person);check('Appointment history retained',len(profile['history'])>=6);check('Membership affiliation remains attached',any(m['clubId']==club['id'] for m in profile['affiliations']))
for status in ['Current','Expiring','Upcoming','Proposed','Pending approval','Scheduled','Active','Completed','Cancelled','Revoked','']:
 call(admin,'/leadership/center?'+urllib.parse.urlencode(dict(status=status,orgId=club['id'])))
for sort in ['name','name-desc','newest']:
 d=call(admin,'/leadership/people?'+urllib.parse.urlencode(dict(sort=sort,orgId=club['id'])));check('People '+sort+' deduplicated',len({p['id'] for p in d['items']})==len(d['items']))
for query in ['search=Demo','search=Service','search=IM-','membershipStatus=Active','page=2','positionId='+positions['Club']['id']]:call(admin,'/leadership/people?'+query)
call(admin,'/leadership/center?status=invalid',expected=400);call(admin,'/leadership/center?from=wrong',expected=400);call(admin,'/leadership/center?from=2026-11-01&to=2025-01-01',expected=400);call(admin,'/leadership/center?orgId='+str(uuid.uuid4()),expected=404)
# Preserve actual conflict and workflow rules.
payload=dict(orgId=club['id'],personId=person,positionId=positions['Club']['id'],startsAt=at(-.005),endsAt=at(25),method='Appointed')
call(admin,'/officer-appointments','POST',payload,409)
call(admin,'/officer-appointments','POST',{**payload,'positionId':positions['National']['id']},400)
call(admin,'/officer-appointments','POST',{**payload,'startsAt':at(30),'endsAt':at(1)},400)
call(admin,'/officer-appointments','POST',{**payload,'method':'Invented'},400)
# A future, same-office reappointment is a separate record, never an overwrite.
if 'future' not in fixture:
 fixture['future']=call(admin,'/officer-appointments','POST',{**payload,'startsAt':at(21),'endsAt':at(386),'notes':'QA reappointment / successor term'});fixturePath.write_text(json.dumps(fixture),encoding='utf-8')
future=call(admin,'/officer-appointments/'+fixture['future']['id']);check('Future term scheduled independently',future['appointment']['status']=='Scheduled');old=call(admin,'/officer-appointments/'+fixture['active-Club']['id']);check('Original term preserved',old['appointment']['id']!=future['appointment']['id'] and old['appointment']['status']=='Active')
succ=call(admin,'/leadership/center?orgId='+club['id']);check('Term expiry identified',succ['summary']['expiring90']>=1);check('Recorded future candidate linked by office/unit',any(n['id']==fixture['future']['id'] for p in succ['succession'] for n in p['candidates']))
# Configure a separate office that requires independent governance approval.
reviewCode='LP-QA-REVIEW';reviewPos=next((p for p in cfg['positions'] if p['code']==reviewCode),None)
if not reviewPos:reviewPos=call(admin,'/officer-positions','POST',dict(code=reviewCode,title='QA Independently Reviewed Advisor',levelId=club['levelId'],maximumHolders=2,mandatory=True,requireActiveMembership=True,approvalRequired=True))
if 'review' not in fixture:
 fixture['review']=call(admin,'/officer-appointments','POST',{**payload,'positionId':reviewPos['id'],'endsAt':at(180)});fixturePath.write_text(json.dumps(fixture),encoding='utf-8')
record=call(admin,'/officer-appointments/'+fixture['review']['id']);request=fixture['review'].get('request')
if record['appointment']['status']=='Proposed':
 submitted=call(admin,'/governance/requests/'+request['id']+'/submit','POST',dict(version=request['version']))
 call(admin,'/governance/requests/'+request['id']+'/decisions','POST',dict(decision='Approve',comment='Self approval must fail',version=submitted['version']),403)
 call(review,'/governance/requests/'+request['id']+'/decisions','POST',dict(decision='Approve',comment='Independent QA appointment review',version=submitted['version']))
record=call(admin,'/officer-appointments/'+fixture['review']['id']);check('Independent approval activates appointment',record['appointment']['status']=='Active')
# Existing interim mechanism, multiple legitimate offices, and no duplicate identity.
if 'interim' not in fixture:
 interim=call(admin,'/officer-positions','POST',dict(code='LP-ACT-'+uuid.uuid4().hex[:8],title='QA Acting Service Office',levelId=club['levelId'],maximumHolders=1,requireActiveMembership=True,approvalRequired=False));fixture['interim']=call(admin,'/officer-appointments','POST',{**payload,'positionId':interim['id'],'method':'Interim','endsAt':at(30)});fixturePath.write_text(json.dumps(fixture),encoding='utf-8')
check('Interim method preserved',call(admin,'/officer-appointments/'+fixture['interim']['id'])['appointment']['method']=='Interim')
# Vacancy follow-up uses existing organization settings and a separate audit record.
v=next(v for v in call(admin,'/leadership/center?orgId='+club['id'])['vacancies']['items'] if v['positionId']!=positions['Club']['id']);note=dict(reason='QA appointment search pending',priority='High',expectedFillingDate=(now+datetime.timedelta(days=14)).date().isoformat(),responsibleAuthority='QA club leadership',previousValue=v['note'])
call(admin,'/leadership/vacancies/'+v['orgId']+'/'+v['positionId'],'PUT',note);call(admin,'/leadership/vacancies/'+v['orgId']+'/'+v['positionId'],'PUT',note,409);history=call(admin,'/leadership/vacancies/'+v['orgId']+'/'+v['positionId']+'/history');check('Vacancy history retained',len(history)>0);check('Unknown vacancy age not fabricated',v['ageDays'] is None or v['since'] is not None)
call(admin,'/leadership/reminders?orgId='+club['id'],'POST',{});again=call(admin,'/leadership/reminders?orgId='+club['id'],'POST',{});check('Daily reminder duplicate prevention',again['queued']==0)
for endpoint in ['/leadership/export?orgId='+club['id'],'/leadership/people/export?orgId='+club['id']]:
 status,body,headers=admin.request(endpoint);check('Scoped CSV '+endpoint,status==200 and b'Person' in body and 'text/csv' in headers.get('Content-Type',''))
for role in ['national','regional','district','zonal','club']:
 c=Client();check(role+' login',c.login(role+'.president@example.test')[0]==200);d=call(c,'/leadership/center');check(role+' units follow authorized root',all(u['id']!=structure['root']['id'] for u in d['units']) if role!='national' else True)
 foreign=next(n['id'] for n in nodes if n['code']=='DEMO-R05')
 if role in ['regional','district','zonal','club']:call(c,'/leadership/center?orgId='+foreign,expected=404)
member=Client();member.login('member.club2@example.test');call(member,'/leadership/center',expected=403)
# Explicit QA reader role: a leadership title does not confer contact privileges.
roles=call(admin,'/roles');reader=next((r for r in roles if r['name']=='QA Leadership Privacy Reader'),None)
if not reader:reader=call(admin,'/roles','POST',dict(name='QA Leadership Privacy Reader',permissions=['organization.view','leadership.view']))
call(admin,'/assignments','POST',dict(email='member.club2@example.test',roleId=reader['id'],orgId=club['id']),204)
ws=call(member,'/workspaces');rw=next(x for x in ws if x['role']=='QA Leadership Privacy Reader');check('Reader role activation',member.activate(rw)[0]==204)
private=call(member,'/leadership/people');check('Reader sees office bearer without contacts',any(p['id']==person for p in private['items']) and all(p['email'] is None and p['phone'] is None for p in private['items']))
pp=call(member,'/leadership/people/'+person);check('Profile personal fields restricted',pp['profile']['birthDate'] is None and pp['profile']['legalName'] is None);check('Profile contacts restricted',not pp['profile']['contacts']);call(member,'/leadership/export',expected=403);call(member,'/leadership/people/export',expected=403);call(member,'/leadership/reminders','POST',{},403)
anon=Client();call(anon,'/leadership/center',expected=401);call(anon,'/leadership/people',expected=401)
check('All leadership operations preserve people count',int(sql('SELECT count(*) FROM "Persons"'))==basePeople)
result=dict(total=len(checks),passed=sum(c['passed'] for c in checks),failed=[c for c in checks if not c['passed']],checks=checks);pathlib.Path('docs/qa/leadership-center-api-results.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='checks'},indent=2));sys.exit(bool(result['failed']))
