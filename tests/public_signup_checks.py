"""Public signup and real president approval tests against the isolated QA clone only."""
import json, os, pathlib, sys, urllib.parse, uuid
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5100'
os.environ['CLUBS_TEST_DATABASE']='clubs_approval_scope_20261114'
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from support import Client,sql,BASE,PASSWORD
assert BASE=='http://127.0.0.1:5100'
checks=[];out=pathlib.Path('docs/qa/public-signup');out.mkdir(parents=True,exist_ok=True)
def check(name,valid,detail=None):
    checks.append(dict(name=name,passed=bool(valid),detail=detail))
    (out/'results.json').write_text(json.dumps(dict(total=len(checks),passed=sum(x['passed'] for x in checks),checks=checks),indent=2),encoding='utf-8')
    assert valid,(name,detail)
def call(c,path,method='GET',data=None,expected=200):
    status,result=c.req(path,method,data);check(method+' '+path,status==expected,None if status==expected else dict(status=status,error=result));return result
def login(email):
    c=Client();status,result=c.login(email);check(email+' login',status==200);c.login_result=result;return c
def org(code):return json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Code"=\''+code+'\''))
club=org('DEMO-R01-D01-Z01-C01');sibling=org('DEMO-R01-D01-Z01-C02');outside=org('DEMO-R01-D01-Z02-C01')
public=Client();prefix=uuid.uuid4().hex[:9]
catalog=call(public,'/public/membership-options')
check('Zones and clubs publicly visible without login',len(catalog['zones'])>0 and catalog['availableClubs']>0 and catalog['signupAvailable'])
check('Public catalog has no contacts, users or member data',not any(k in json.dumps(catalog).lower() for k in ['password','phone','email','personid','userid','membernumber']))
filtered=call(public,'/public/membership-options?zoneId='+club['ParentId'])
check('Selected zone contains only its clubs',all(c['zoneId']==club['ParentId'] for c in filtered['clubs']))
found=call(public,'/public/membership-options?zoneId='+club['ParentId']+'&search='+club['Code'])
check('Club code search selects intended club',any(c['id']==club['Id'] for c in found['clubs']))
call(public,'/public/membership-options?zoneId='+str(uuid.uuid4()),expected=404)

def signup(label,target=club,extra=None,expected=202):
    email='signup.'+prefix+'.'+label+'@example.test'
    body=dict(name='Signup QA '+prefix+' '+label,email=email,phone='+919876543210',password=PASSWORD,confirmation=PASSWORD,agreed=True,zoneId=target['ParentId'],clubId=target['Id'])
    body.update(extra or {})
    result=call(public,'/public/membership-signup','POST',body,expected)
    if expected!=202:return None
    check('Public response remains pending',result['status']=='Pending')
    user=json.loads(sql('SELECT row_to_json(u) FROM "Users" u WHERE "Email"=\''+email+'\''))
    app=json.loads(sql('SELECT row_to_json(a) FROM "MembershipApplications" a WHERE "ApplicantUserId"=\''+user['Id']+'\''))
    return user,app,body

before=sql('SELECT count(*) FROM "Users"')
signup('bad-zone',extra={'zoneId':outside['ParentId']},expected=400)
signup('weak-password',extra={'password':'short','confirmation':'short'},expected=400)
signup('no-consent',extra={'agreed':False},expected=400)
signup('unknown-club',extra={'clubId':str(uuid.uuid4())},expected=400)
check('Invalid signup creates no accounts',sql('SELECT count(*) FROM "Users"')==before)

user,application,body=signup('club',extra={'platformAdmin':True,'status':'Active','verified':True,'role':'Association Super Admin'})
uid=user['Id'];aid=application['Id']
check('New user is unverified and never platform admin',not user['Verified'] and not user['PlatformAdmin'])
check('Application is submitted and awaiting president approval',application['Status']=='Submitted')
check('Signup grants no active membership or roles',sql('SELECT count(*) FROM "Memberships" WHERE "SourceApplicationId"=\''+aid+'\'')=='0' and sql('SELECT count(*) FROM "Assignments" WHERE "UserId"=\''+uid+'\'')=='0')
workflow=json.loads(sql('SELECT row_to_json(w) FROM "MembershipWorkflows" w WHERE "Id"=\''+application['WorkflowId']+'\''))
check('Public signup uses dedicated workflow',workflow['Kind']=='PublicSignup')
check('Club or zone approval requires one decision only',sql('SELECT count(*)||\'|\'||min("RequiredApprovals") FROM "MembershipApprovalStages" WHERE "WorkflowId"=\''+workflow['Id']+'\'')=='1|1')
reviewers=sql('SELECT r."Name" FROM "MembershipStageRoles" sr JOIN "Roles" r ON r."Id"=sr."RoleId" JOIN "MembershipApprovalStages" s ON s."Id"=sr."StageId" WHERE s."WorkflowId"=\''+workflow['Id']+'\'')
check('President reviewer roles configured',all(r in reviewers for r in ['Club President','Zonal President']) and 'Secretary' not in reviewers)
recipients=sql('SELECT u."Email" FROM "Mails" m JOIN "Users" u ON u."Id"=m."UserId" WHERE m."Kind"=\'approval.request\' AND m."OrgId"=\''+club['Id']+'\' AND m."Body" LIKE \'%Club or zone president approval%\'')
check('Club and zone presidents notified',all(e in recipients for e in ['club.president@example.test','zonal.president@example.test']))

original_hash=user['PasswordHash'];again={**body,'email':body['email'].upper(),'password':'ChangedButNotApplied123!','confirmation':'ChangedButNotApplied123!'}
call(public,'/public/membership-signup','POST',again,202)
check('Repeated signup cannot replace password or duplicate applications',sql('SELECT "PasswordHash" FROM "Users" WHERE "Id"=\''+uid+'\'')==original_hash and sql('SELECT count(*) FROM "MembershipApplications" WHERE "ApplicantUserId"=\''+uid+'\'')=='1')
applicant=Client();check('Unverified account cannot sign in',applicant.login(body['email'])[0]==401)
link=sql('SELECT "Link" FROM "Mails" WHERE "UserId"=\''+uid+'\' AND "Kind"=\'verify\' ORDER BY "ExpiresAt" DESC LIMIT 1')
token=urllib.parse.parse_qs(urllib.parse.urlsplit(link).query)['token'][0]
call(public,'/auth/complete','POST',dict(token=token,password=''))
applicant=login(body['email']);check('Pending applicant login routes to application tracking',applicant.login_result['pendingMembership']);check('Pending applicant has no workspace',call(applicant,'/workspaces')==[])
own=call(applicant,'/membership/applications?mine=true');check('Pending applicant can track application without a workspace',any(a['id']==aid for a in own['items']))
call(applicant,'/members',expected=403)
call(applicant,'/membership/applications/'+aid+'/decisions','POST',dict(decision='Approve',comment='',version=1),403)

president=login('club.president@example.test');zone=login('zonal.president@example.test');admin=login('saisatish@indianservers.com')
def approve(c,app):
    record=call(c,'/membership/applications/'+app['Id']);check('President can decide pending signup',record['canDecide'])
    call(c,'/membership/applications/'+app['Id']+'/decisions','POST',dict(decision='Approve',comment='Public signup QA approval',version=record['application']['version']))
    check('Single presidential approval activates membership',sql('SELECT "Status" FROM "MembershipApplications" WHERE "Id"=\''+app['Id']+'\'')=='Approved' and sql('SELECT count(*) FROM "Memberships" WHERE "SourceApplicationId"=\''+app['Id']+'\' AND "Status"=\'Active\'')=='1')
approve(president,application)
check('Membership role granted only after approval',sql('SELECT count(*) FROM "Assignments" WHERE "UserId"=\''+uid+'\'')=='1')
check('Approved applicant obtains member workspace',any(w['role']=='Club Member' for w in call(applicant,'/workspaces')))

zuser,zapp,_=signup('zone',sibling)
call(president,'/membership/applications/'+zapp['Id'],expected=404)
call(president,'/membership/applications/'+zapp['Id']+'/decisions','POST',dict(decision='Approve',comment='',version=1),404)
approve(zone,zapp)
faruser,farapp,_=signup('foreign-zone',outside)
call(zone,'/membership/applications/'+farapp['Id'],expected=404)
call(zone,'/membership/applications/'+farapp['Id']+'/decisions','POST',dict(decision='Approve',comment='',version=1),404)
check('Foreign zone attempt leaves signup pending',sql('SELECT "Status" FROM "MembershipApplications" WHERE "Id"=\''+farapp['Id']+'\'')=='Submitted')
approve(admin,farapp)

ruser,rapp,_=signup('reject')
call(president,'/membership/applications/'+rapp['Id']+'/decisions','POST',dict(decision='Reject',comment='QA rejection',version=1))
check('Rejection creates no membership or role',sql('SELECT "Status" FROM "MembershipApplications" WHERE "Id"=\''+rapp['Id']+'\'')=='Rejected' and sql('SELECT count(*) FROM "Assignments" WHERE "UserId"=\''+ruser['Id']+'\'')=='0')

# Inactive ancestors remove every descendant club from the public directory.
sql('UPDATE "Orgs" SET "Status"=\'Inactive\' WHERE "Id"=\''+outside['ParentId']+'\'')
try:
    options=call(public,'/public/membership-options')
    check('Inactive zone hidden',all(z['id']!=outside['ParentId'] for z in options['zones']))
    signup('inactive-zone',outside,expected=400)
finally:sql('UPDATE "Orgs" SET "Status"=\'Active\' WHERE "Id"=\''+outside['ParentId']+'\'')
check('Signup audit records pending state and consent',sql('SELECT count(*) FROM "Audits" WHERE "Action"=\'membership.public-signup\' AND "Resource"=\''+aid+'\' AND "Changes" LIKE \'%consentRecorded%\'')=='1')
print(json.dumps(dict(total=len(checks),passed=sum(x['passed'] for x in checks)),indent=2))
