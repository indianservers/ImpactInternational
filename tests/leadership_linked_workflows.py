import json,os,sys,pathlib,datetime,uuid
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE']=='clubs_impact_dues_upgrade_20261113'
r=[]
def check(name,ok,data=None):
 r.append(dict(scenario=name,passed=bool(ok),detail=data if not ok else None))
 if not ok:print('FAIL',name,str(data)[:250])
def call(c,p,m='GET',body=None,status=200):
 s,d=c.req(p,m,body);check(m+' '+p,s==status,d);return d
admin=Client();admin.login('saisatish@indianservers.com');review=Client();review.login('admin@example.test');f=json.loads(pathlib.Path('.infra/leadership-center-fixtures.json').read_text());club='18ee9a12-bcc5-464f-bae4-1377038a0162';cfg=call(admin,'/governance/options');p=call(admin,'/officer-appointments/'+f['active-Club']['id'])['appointment'];person=p['personId'];now=datetime.datetime.now(datetime.timezone.utc)
def at(days):return (now+datetime.timedelta(days=days)).isoformat()
if 'committee' not in f:
 f['committee']=call(admin,'/committees','POST',dict(orgId=club,name='Leadership QA committee',code='LP-QA-COMMITTEE',purpose='Validate central person leadership and committee integration',startsAt=at(-.005),endsAt=at(180)));pathlib.Path('.infra/leadership-center-fixtures.json').write_text(json.dumps(f))
request=f['committee']['request'];c=call(admin,'/committees/'+f['committee']['id'])
if c['committee']['status']=='Proposed':
 req=call(admin,'/governance/requests/'+request['id']+'/submit','POST',dict(version=request['version']));call(review,'/governance/requests/'+request['id']+'/decisions','POST',dict(decision='Approve',comment='Independent committee QA review',version=req['version']))
role=next(x for x in cfg['committeeRoles'] if x['name']=='Member');c=call(admin,'/committees/'+f['committee']['id'])
if not any(m.get('personId')==person for m in c.get('members',[])):
 call(admin,'/committees/'+f['committee']['id']+'/members','POST',dict(personId=person,roleId=role['id'],startsAt=at(-.001),endsAt=at(179),responsibilities='QA community coordination'))
profile=call(admin,'/leadership/people/'+person);check('Committee and appointment share central person',any(m['committeeId']==f['committee']['id'] for m in profile['committees']))
center=call(admin,'/leadership/center?orgId='+club);check('Participation counts real active members',sum(x['people'] for x in center['committeeParticipation'])>=1)
if 'term' not in f:
 f['term']=call(admin,'/leadership-terms','POST',dict(orgId=club,name='Leadership QA annual term',calendar='Custom',startsAt=at(-.01),endsAt=at(365),timeZone='Asia/Kolkata'));pathlib.Path('.infra/leadership-center-fixtures.json').write_text(json.dumps(f))
if 'termAppointment' not in f:
 pos=call(admin,'/officer-positions','POST',dict(code='LP-TERM-'+uuid.uuid4().hex[:8],title='QA Term Service Officer',levelId=p['positionId'] and next(l['id'] for l in cfg['levels'] if l['isClub']),maximumHolders=1,mandatory=False,approvalRequired=False));f['termAppointment']=call(admin,'/officer-appointments','POST',dict(orgId=club,personId=person,positionId=pos['id'],termId=f['term']['id'],startsAt=at(-.001),endsAt=at(180),method='Appointed'));pathlib.Path('.infra/leadership-center-fixtures.json').write_text(json.dumps(f))
filtered=call(admin,'/leadership/center?termId='+f['term']['id']);check('Term filter matches appointment',filtered['total']==1 and filtered['items'][0]['id']==f['termAppointment']['id'])
people=call(admin,'/leadership/people?termId='+f['term']['id']);check('People term filter follows actual appointments',people['total']==1 and people['items'][0]['id']==person)
call(admin,'/leadership-handovers','POST',dict(outgoingAppointmentId=f['active-Club']['id'],incomingAppointmentId=f['future']['id'],notes='Same-person reappointment is not a handover',items=['Review']),400)
result=dict(total=len(r),passed=sum(x['passed'] for x in r),failed=[x for x in r if not x['passed']],checks=r);pathlib.Path('docs/qa/leadership-linked-workflows-results.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='checks'},indent=2));sys.exit(bool(result['failed']))
