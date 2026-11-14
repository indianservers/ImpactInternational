"""Opt-in acceptance test: creates three clearly named users in the local demo.

Approves two through real president sessions and retains one pending scope-test user.
Email verification uses the local queued links; external delivery is not asserted.
"""
import datetime, json, os, pathlib, sys, urllib.parse, uuid
assert '--create-demo-users' in sys.argv, 'Explicit --create-demo-users is required.'
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:1234'
os.environ['CLUBS_TEST_DATABASE']='clubs_impact_dues_upgrade_20261113'
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from support import Client,sql,BASE,PASSWORD
assert BASE=='http://127.0.0.1:1234'
out=pathlib.Path('docs/qa/demo-signup-flow');out.mkdir(parents=True,exist_ok=True)
checks=[];accounts=[]
resume = '--resume-demo-users' in sys.argv
if resume:
    previous=json.loads((out/'results.json').read_text(encoding='utf-8'))
    accounts=previous['accounts'];checks=[c for c in previous['checks'] if c['passed']]
    assert len(accounts)==3 and all(a['status']=='Pending approval' for a in accounts), 'Resume only this unfinished demo run.'

def save():
    result=dict(total=len(checks),passed=sum(c['passed'] for c in checks),failed=[c for c in checks if not c['passed']],
        environment='Live local demo at localhost:1234',emailVerification='Locally queued verification links; external email delivery not tested',accounts=accounts,checks=checks)
    (out/'results.json').write_text(json.dumps(result,indent=2),encoding='utf-8');return result
def check(name,valid,detail=None):
    checks.append(dict(name=name,passed=bool(valid),detail=detail));save()
    assert valid,(name,detail)
def call(c,path,method='GET',data=None,expected=200):
    status,result=c.req(path,method,data);check(method+' '+path,status==expected,None if status==expected else dict(status=status,error=result));return result
def login(email):
    c=Client();status,result=c.login(email);check(email+' login',status==200);c.login_result=result;return c
def org(code):return json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Code"=\''+code+'\''))
clubs=[org(c) for c in ['DEMO-R01-D01-Z01-C01','DEMO-R01-D01-Z01-C02','DEMO-R01-D01-Z02-C01']]
public=Client();methods=call(public,'/auth/methods')
check('Demo uses password member login',methods['memberOtpEnabled'] is False)
options=call(public,'/public/membership-options')
check('Public zones and clubs available',options['signupAvailable'] and bool(options['zones']))
if not resume:
    run=uuid.uuid4().hex[:7]
    for index,(label,target) in enumerate(zip(['Club approval','Zone approval','Pending outside zone'],clubs)):
        email=f'demo.signup.{["club","zone","pending"][index]}.20261114.{run}@example.test'
        body=dict(name='Demo Signup — '+label,email=email,phone='+919876543210',password=PASSWORD,confirmation=PASSWORD,agreed=True,zoneId=target['ParentId'],clubId=target['Id'])
        response=call(public,'/public/membership-signup','POST',body,202)
        check(label+' signup reports pending',response['status']=='Pending')
        user=json.loads(sql('SELECT row_to_json(u) FROM "Users" u WHERE "Email"=\''+email+'\''))
        app=json.loads(sql('SELECT row_to_json(a) FROM "MembershipApplications" a WHERE "ApplicantUserId"=\''+user['Id']+'\''))
        account=dict(label=label,email=email,userId=user['Id'],applicationId=app['Id'],clubId=target['Id'],club=target['Name'],zoneId=target['ParentId'],status='Pending approval')
        accounts.append(account);save()
        check(label+' has pending submitted application',app['Status']=='Submitted')
        check(label+' has no membership or role before approval',sql('SELECT count(*) FROM "Memberships" WHERE "SourceApplicationId"=\''+app['Id']+'\'')=='0' and sql('SELECT count(*) FROM "Assignments" WHERE "UserId"=\''+user['Id']+'\'')=='0')
        unverified=Client();check(label+' unverified sign-in blocked',unverified.login(email)[0]==401)
        link=sql('SELECT "Link" FROM "Mails" WHERE "UserId"=\''+user['Id']+'\' AND "Kind"=\'verify\' ORDER BY "ExpiresAt" DESC LIMIT 1')
        token=urllib.parse.parse_qs(urllib.parse.urlsplit(link).query)['token'][0]
        call(public,'/auth/complete','POST',dict(token=token,password=''))
        pending=login(email);check(label+' login identifies pending application',pending.login_result['pendingMembership'])
        check(label+' has no pending workspace',call(pending,'/workspaces')==[])
        own=call(pending,'/membership/applications?mine=true');check(label+' can track own pending status',any(a['id']==app['Id'] and a['status']=='Submitted' for a in own['items']))
        portal=call(pending,'/membership/me');check(label+' portal has no active memberships',not portal['memberships'])
        call(pending,'/members',expected=403)
        call(pending,'/membership/applications/'+app['Id']+'/decisions','POST',dict(decision='Approve',comment='',version=app['Version']),403)
club=login('club.president@example.test');zone=login('zonal.president@example.test');admin=login('saisatish@indianservers.com')
# Explicitly activate the intended reviewer roles before testing decisions.
for c,role in [(club,'Club President'),(zone,'Zonal President')]:
    choices=call(c,'/workspaces');w=next(w for w in choices if w['role']==role);call(c,'/workspaces/'+w['id']+'/activate','POST',{},204)
    check(role+' workspace has approval permission','members.approve' in w['permissions'])

clubInbox=call(club,'/membership/applications?status=Pending')['items']
zoneInbox=call(zone,'/membership/applications?status=Pending')['items']
check('Club president inbox excludes sibling and foreign clubs',accounts[0]['applicationId'] in {a['id'] for a in clubInbox} and not any(a['id'] in {accounts[1]['applicationId'],accounts[2]['applicationId']} for a in clubInbox))
check('Zone president inbox includes both own-zone clubs and excludes other zone',all(a['applicationId'] in {r['id'] for r in zoneInbox} for a in accounts[:2]) and accounts[2]['applicationId'] not in {r['id'] for r in zoneInbox})
for c,a,label in [(club,accounts[1],'Club president sibling-club denial'),(club,accounts[2],'Club president foreign-zone denial'),(zone,accounts[2],'Zone president foreign-zone denial')]:
    call(c,'/membership/applications/'+a['applicationId'],expected=404)
    call(c,'/membership/applications/'+a['applicationId']+'/decisions','POST',dict(decision='Approve',comment='Must be denied outside scope',version=1),404)
    check(label+' preserves pending state',sql('SELECT "Status" FROM "MembershipApplications" WHERE "Id"=\''+a['applicationId']+'\'')=='Submitted')

for c,a,approver in [(club,accounts[0],'club.president@example.test'),(zone,accounts[1],'zonal.president@example.test')]:
    path='/membership/applications/'+a['applicationId'];detail=call(c,path);check(a['label']+' can be approved by intended president',detail['canDecide'])
    call(c,path+'/decisions','POST',dict(decision='Approve',comment='Authorized end-to-end demo signup acceptance test',version=detail['application']['version']))
    approved=call(c,path);check(a['label']+' needs one presidential approval',approved['application']['status']=='Approved' and len(approved['decisions'])==1)
    call(c,path+'/decisions','POST',dict(decision='Approve',comment='Duplicate approval must be rejected',version=approved['application']['version']),409)
    check(a['label']+' creates one membership only',sql('SELECT count(*) FROM "Memberships" WHERE "SourceApplicationId"=\''+a['applicationId']+'\'')=='1')
    actor=sql('SELECT u."Email" FROM "MembershipApprovals" d JOIN "Users" u ON u."Id"=d."ActorId" WHERE d."ApplicationId"=\''+a['applicationId']+'\'')
    check(a['label']+' audit identifies actual approving president',actor==approver)
    member=login(a['email']);check(a['label']+' fresh login is no longer pending',not member.login_result['pendingMembership'])
    workspaces=call(member,'/workspaces');check(a['label']+' receives only selected club member workspace',len(workspaces)==1 and workspaces[0]['role']=='Club Member' and workspaces[0]['orgId']==a['clubId'])
    me=call(member,'/auth/me');check(a['label']+' member workspace automatically selected',me['workspaceId']==workspaces[0]['id'])
    portal=call(member,'/membership/me');memberships=portal['memberships']
    check(a['label']+' member portal returns active membership',len(memberships)==1 and memberships[0]['status']=='Active' and memberships[0]['clubId']==a['clubId'] and bool(memberships[0]['number']))
    a.update(status='Active',approvedBy=approver,membershipNumber=memberships[0]['number'],membershipId=memberships[0]['id'])
    call(member,'/membership/applications',expected=403)
    call(member,'/roles',expected=403)
    call(member,'/membership/applications/'+accounts[2]['applicationId']+'/decisions','POST',dict(decision='Approve',comment='',version=1),403)
    own=call(member,'/membership/applications?mine=true');check(a['label']+' own application now approved',any(r['id']==a['applicationId'] and r['status']=='Approved' for r in own['items']))
    save()

pending=login(accounts[2]['email']);check('Scope-test user remains pending after both successful approvals',pending.login_result['pendingMembership'] and call(pending,'/workspaces')==[])
check('Platform admin retains access to other-zone pending application',call(admin,'/membership/applications/'+accounts[2]['applicationId'])['canDecide'])
for a in accounts:
    check(a['label']+' signup audit exists',sql('SELECT count(*) FROM "Audits" WHERE "Action"=\'membership.public-signup\' AND "Resource"=\''+a['applicationId']+'\'')=='1')
result=save();print(json.dumps({k:v for k,v in result.items() if k!='checks'},indent=2))
