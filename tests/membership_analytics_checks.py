"""Scoped analytics: deterministic historical fixtures, totals, filters, exports and authorization."""
import datetime,json,os,pathlib,sys,time,uuid,urllib.parse
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE']=='clubs_impact_dues_upgrade_20261113'
results=[];timings=[]
def check(name,passed,detail=None):
 results.append(dict(scenario=name,passed=bool(passed),detail=detail))
 if not passed:print('FAIL',name,str(detail)[:300],flush=True)
def get(c,name,query=None,status=200,export=False):
 t=time.perf_counter();s,d=c.req('/membership/analytics'+('/export' if export else '')+'?'+urllib.parse.urlencode(query or {}));ms=round((time.perf_counter()-t)*1000,1);timings.append(ms);check(name,s==status,dict(status=s,ms=ms,error=d if s>=400 else None));return d
admin=Client();check('Admin login',admin.login('saisatish@indianservers.com')[0]==200)
opts=admin.req('/membership/options')[1];category=opts['categories'][0]['id'];club=next(o['id'] for o in opts['clubs'] if o['name']=='Demo Region 01 District 01 Zone 01 Club 05');foreign=next(o['id'] for o in opts['clubs'] if 'Region 02 District 01 Zone 01 Club 01'==o['name'].removeprefix('Demo '))
# Fixtures are reused on repeated runs. History dates describe simulated QA events only.
fixture=pathlib.Path('.infra/analytics-history-fixtures.json')
if fixture.exists():f=json.loads(fixture.read_text())
else:
 f=[]
 for i,date in enumerate(['2025-12-01','2026-01-15','2026-05-01','2026-06-01']):
  s,d=admin.req('/members','POST',dict(person=dict(legalName='Analytics Historical QA '+str(i),displayName=''),clubId=club,categoryId=category,joinedAt=date+'T00:00:00Z',requestKey=str(uuid.uuid4()),duplicateDecision='CreateSeparate'));assert s==200,d;f.append(d)
 fixture.write_text(json.dumps(f))
tenant=sql(f'''SELECT "TenantId" FROM "Memberships" WHERE "Id"='{f[0]['id']}' ''');actor=admin.req('/auth/me')[1]['id']
# Append only synthetic fixture history. Existing history remains immutable.
for i,m in enumerate(f):
 mid=m['id']
 if sql(f'''SELECT count(*) FROM "MembershipHistory" WHERE "MembershipId"='{mid}' AND "Reason"='Synthetic analytics fixture' ''')!='0':continue
 initial=['2025-12-01','2026-01-15','2026-05-01','2026-06-01'][i];ends="'2026-09-01T00:00:00Z'" if i==0 else "'2026-07-01T00:00:00Z'" if i==2 else 'NULL';state='Transferred' if i==1 else 'Active'
 sql(f'''UPDATE "Memberships" SET "Status"='{state}',"EndsAt"={"'2027-01-01T00:00:00Z'" if i==0 else ends} WHERE "Id"='{mid}'; INSERT INTO "MembershipHistory" ("Id","TenantId","MembershipId","OldStatus","NewStatus","Reason","EffectiveAt","RecordedAt","ActorId","CategoryId","EndsAt") VALUES('{uuid.uuid4()}','{tenant}','{mid}','','Active','Synthetic analytics fixture','{initial}T00:00:01Z','{initial}T00:00:01Z','{actor}','{category}',{ends});''')
 def history(old,new,at,valid='NULL'):
  sql(f'''INSERT INTO "MembershipHistory" ("Id","TenantId","MembershipId","OldStatus","NewStatus","Reason","EffectiveAt","RecordedAt","ActorId","CategoryId","EndsAt") VALUES('{uuid.uuid4()}','{tenant}','{mid}','{old}','{new}','Synthetic analytics fixture','{at}','{at}','{actor}','{category}',{valid});''')
 if i==0:
  history('Active','Active','2026-08-01T00:00:00Z',"'2027-01-01T00:00:00Z'")
  sql(f'''INSERT INTO "MembershipRenewals" ("Id","TenantId","MembershipId","ActorId","RequestKey","PreviousEnd","NewEnd","At") VALUES('{uuid.uuid4()}','{tenant}','{mid}','{actor}','analytics-test','2026-09-01T00:00:00Z','2027-01-01T00:00:00Z','2026-08-01T00:00:00Z');''')
 if i==1:history('Active','Inactive','2026-04-01T00:00:00Z');history('Inactive','Transferred','2026-09-01T00:00:00Z')
base=dict(period='custom',**{'from':'2026-01-01','to':'2026-10-31'},clubId=club)
d=get(admin,'Historical controlled scope',base)
check('Distinct people correct',d['summary']['total']==4,d['summary']);check('Active honors historical expiry',d['summary']['active']==2);check('Opening cohort excludes new people',d['summary']['opening']==1);check('Retained opening cohort',d['summary']['retained']==1);check('Period acquisitions exclude pre-period member',d['summary']['new']==3);check('Recorded renewal events',d['renewals']['renewed']==1);check('Eligibility uses previous expiry',d['renewals']['eligible']==2)
check('Snapshot expiry counted in trend',d['trend'][-1]['expired']==1);check('Transfer history bucket',next(r for r in d['trend'] if r['date']=='2026-09-01')['transfers']==1);check('Inactive status at May boundary',next(r for r in d['trend'] if r['date']=='2026-05-01')['inactive']==1)
co=get(admin,'Cohort workspace',dict(base,workspace='cohorts'));cells=co['cohorts'];check('Observed retained cohort',next(x for x in cells if x['cohort']=='2026-01' and x['months']==1)['retained']==1);check('Observed churn cohort',next(x for x in cells if x['cohort']=='2026-01' and x['months']==3)['retained']==0);check('Incomplete observation null',next(x for x in cells if x['cohort']=='2026-06' and x['months']==12)['retained'] is None)
o=next(o for o in d['organizations'] if o['id']==club);check('Org opening and retention reconcile',o['opening']==1 and o['retained']==1);check('Org renewal evidence',o['renewed']==1 and o['eligible']==2)
l=get(admin,'Lifecycle workspace',dict(base,workspace='lifecycle'));check('Real transitions preserved',any(e['source']=='Inactive' and e['target']=='Transferred' and e['count']==1 for e in l['flows']));check('No invented application flow',all('Application' not in e['source'] for e in l['flows']))
for period in ['7d','30d','90d','quarter','previous-quarter','year','previous-year','all']:
 v=get(admin,'Period '+period,dict(period=period));check('Reconciled categories '+period,sum(r['count'] for r in v['distributions'] if r['dimension']=='category')==v['summary']['affiliations'])
for bucket in ['day','week','month','quarter','year']:
 v=get(admin,'Aggregation '+bucket,dict(base,bucket=bucket));check('Trend bounded '+bucket,0<len(v['trend'])<=366)
for workspace in ['overview','growth','organization','cohorts','lifecycle','renewals','categories','geography','engagement','forecasting','actions','network']:
 v=get(admin,'Workspace '+workspace,dict(base,workspace=workspace));check('Same global filters '+workspace,v['summary']['total']==4)
for key,val in [('nationalId','DEMO-NAT-01'),('regionId','DEMO-R01'),('districtId','DEMO-R01-D01'),('zoneId','DEMO-R01-D01-Z01'),('clubId','DEMO-R01-D01-Z01-C05')]:
 id=sql(f'''SELECT "Id" FROM "Orgs" WHERE "Code"='{val}' ''');v=get(admin,'Hierarchy '+key,{key:id});check('Hierarchy narrows '+key,v['summary']['total']<=get(admin,'Parent total reference '+key)['summary']['total'])
for key,val in [('categoryId',category),('status','Transferred'),('country','India'),('state',''),('membershipType','Community'),('joinedFrom','2026-05-01'),('joinedTo','2026-06-30'),('renewalFrom','2026-06-01'),('renewalTo','2026-10-31')]:get(admin,'Dimension '+key,dict(base,**{key:val}))
empty=get(admin,'Empty filter',dict(country='Unrecorded-country'));check('Empty totals remain zero',empty['summary']['total']==0 and not empty['cohorts'])
for name,query in [('Invalid period',dict(period='bad')),('Invalid bucket',dict(bucket='minute')),('Invalid date',dict(period='custom',**{'from':'bad'})),('Future end',dict(period='custom',to='2099-01-01')),('Reversed dates',dict(period='custom',**{'from':'2026-10-01','to':'2026-01-01'})),('Invalid id',dict(regionId='invalid')),('Invalid status',dict(status='bad')),('Daily too large',dict(period='custom',**{'from':'2000-01-01'},bucket='day')),('Reversed joining dates',dict(joinedFrom='2026-06-01',joinedTo='2026-01-01')),('Unknown workspace',dict(workspace='bad'))]:get(admin,name,query,400)
check('No private contact payload','@example.test' not in json.dumps(d) and 'legalName' not in json.dumps(d))
for email,expected in [('international.president@example.test',200),('national.president@example.test',200),('regional.president@example.test',200),('district.president@example.test',200),('zonal.president@example.test',200),('club.president@example.test',200),('member.club1@example.test',403)]:
 c=Client();check('Login '+email,c.login(email)[0]==200);v=get(c,'Role analytics '+email,status=expected)
 if expected==200 and email.startswith(('regional','district','zonal','club')):
  get(c,'Reject foreign scope '+email,dict(clubId=foreign),404)
  check('No foreign org metadata '+email,all(not o['name'].startswith('Demo Region 02') for o in v['organizations']))
  exp=get(c,'Scoped export '+email,export=True,status=200 if v['capabilities']['export'] else 403)
  if v['capabilities']['export']:check('Export has no foreign unit '+email,isinstance(exp,bytes) and b'Demo Region 02' not in exp)
 if expected==403:get(c,'Member export denied',export=True,status=403)
get(Client(),'Unauthenticated analytics denied',status=401);get(admin,'Invalid category tenant',dict(categoryId=str(uuid.uuid4())),404)
export=get(admin,'Authorized filtered CSV',base,export=True);check('CSV content and headers',isinstance(export,bytes) and b'dataset,key,value' in export and b'membership.summary.total' in export)
check('Payload bounded',len(json.dumps(d))<500000,len(json.dumps(d)));check('Database membership count preserved outside QA',sql('SELECT count(*) FROM "Memberships"','clubs_impact')=='3126')
pathlib.Path('docs/qa/membership-analytics-results.json').write_text(json.dumps(dict(total=len(results),passed=sum(r['passed'] for r in results),results=results,fixtureClub=club,fixtureMembers=f,timings=dict(minMs=min(timings),maxMs=max(timings),medianMs=sorted(timings)[len(timings)//2])),indent=2))
print(json.dumps(dict(total=len(results),passed=sum(r['passed'] for r in results),failed=[r for r in results if not r['passed']]),indent=2));assert all(r['passed'] for r in results)
