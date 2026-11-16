"""Run against the ICI QA server after ici_roles_checks.py creates its fixtures.

Exercises every role through authenticated requests; never targets the app database.
"""
import json,pathlib,uuid,datetime
import support
support.BASE='http://127.0.0.1:5090'
support.PASSWORD='Ici-Test-Password-2026!'
DB='clubs_ici_roles_qa_20261116'
fixture=json.loads(pathlib.Path('.infra/ici-qa-actors.json').read_text())
tag=fixture['tag'];suffix=uuid.uuid4().hex[:8]
sql=lambda q:support.sql(q,DB)
results=[]
def check(name,ok,detail=None):
 results.append({'test':name,'passed':bool(ok),'detail':detail if not ok else None})
 pathlib.Path('test-results/ici-role-access-regressions.json').write_text(json.dumps(results,indent=2))
 print(('PASS ' if ok else 'FAIL ')+name,flush=True)
def good(c,path,method='GET',data=None):
 status,body=c.req(path,method,data);assert status in [200,204],(path,status,body);return body
def login(email,role):
 c=support.Client();good(c,'/auth/login','POST',{'email':email,'password':support.PASSWORD})
 w=next(w for w in good(c,'/workspaces') if w['role']==role)
 good(c,'/workspaces/'+w['id']+'/activate','POST',{});return c,w
admin,_=login('admin@example.test','Association Super Admin')
org=lambda code:sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\''+code+"'")
root=org('IMPACT-HQ');nat=org('DEMO-NAT-01');r1=org('DEMO-R01');r2=org('DEMO-R02');c1=org('DEMO-R01-D01-Z01-C01');c2=org('DEMO-R02-D01-Z01-C01')
role_emails={
 'Executive Board':fixture['emails']['board'],
 'Office Bearers':fixture['emails']['office'],
 'National President':'ici-incoming-np-'+tag+'@example.test',
 'National Secretary':fixture['emails']['secretary'],
 'National Treasurer':fixture['emails']['treasurer'],
 'National Vice President':fixture['emails']['nvp1'],
 'National Director':fixture['emails']['training'],
 'National Coordinator':fixture['emails']['eps'],
 'Regional President':'ici-incoming-rp-'+tag+'@example.test',
 'Regional Director':fixture['emails']['rd'],
 'Club President':'club.president@example.test',
}
clients={};workspaces={}
for role,email in role_emails.items():
 client,w=login(email,role);clients[role]=client;workspaces[role]=w
 context=good(client,'/ici/context')
 route=good(client,'/dashboard?view=route')
 check(role+' signs in to correct dashboard and role context',context['role']==role and route['role']==role)
 check(role+' has working activity, request and own-profile views',all(client.req(path)[0]==200 for path in ['/ici/reports','/ici/requests','/ici/profile']))
 check(role+' cannot request the platform dashboard',client.req('/dashboard?view=platform')[0]==403)
 check(role+' cannot spoof a different dashboard role',client.req('/dashboard?view=route&roleId='+str(uuid.uuid4()))[0]==403)
 check(role+' cannot enter communications or administration',all(client.req(path)[0]==403 for path in ['/communications/member-scope','/roles','/assignments','/settings']))
 check(role+' member directory matches its permission',client.req('/members')[0]==(200 if 'members.view' in w['permissions'] else 403))
 check(role+' transfer view matches its permission',client.req('/membership/transfers')[0]==(200 if 'members.transfer.approve' in w['permissions'] else 403))
 check(role+' team view matches its permission',client.req('/ici/team')[0]==(200 if 'ici.team' in w['permissions'] else 403))
 check(role+' bank editing availability matches the source',good(client,'/ici/profile')['bankAllowed']==(role in ['Club President','Regional President']))
 check(role+' cannot directly configure leadership coverage',client.req('/ici/team/'+fixture['assignments']['office']['id']+'/scope','PUT',{'portfolio':'','slot':'','regionIds':[r1]})[0]==403)

cp=clients['Club President'];rp=clients['Regional President'];np=clients['National President'];nd=clients['National Director'];eb=clients['Executive Board'];nvp=clients['National Vice President']
for role in ['Club President','Regional President','National Secretary','Office Bearers','National Vice President']:
 client=clients[role];outside=c1 if role=='Office Bearers' else c2
 check(role+' cannot expand its report using another region ID',client.req('/ici/reports?orgId='+outside)[0]==404)
 check(role+' cannot expand its dashboard using another region ID',client.req('/dashboard?view=scoped&orgId='+outside)[0]==404)

now=datetime.datetime.now(datetime.timezone.utc)
def event(kind='Training',program='',orgId=c1,client=cp):
 start=now-datetime.timedelta(days=1)
 return good(client,'/events','POST',{'orgId':orgId,'typeId':'00000000-0000-0000-0000-000000000000','title':'QA Access '+suffix+' '+kind+' '+program,'description':'QA role access regression activity','startsAt':start.isoformat(),'endsAt':(start+datetime.timedelta(hours=2)).isoformat(),'registrationStartsAt':(start-datetime.timedelta(days=2)).isoformat(),'registrationEndsAt':start.isoformat(),'timeZone':'Asia/Kolkata','venue':'QA Hall','activityType':kind,'activityProgram':program,'activityLevel':'Region' if orgId==r1 else 'Club','activityClubId':c1,'projectCoordinator':'QA Coordinator','coordinatorPhone':'+919000000001','coordinatorEmail':'qa@example.test'})
events={kind:event(kind) for kind in ['Business','Leadership','Service','Training','Nari Cell']}
programs={kind:event('Service',kind) for kind in ['Mini & Mega Impact','EPS','Young Minds','Skill Proficiency','Social Media','National pro']}
def configure(label,portfolio,regions=None):
 row=fixture['assignments'][label]
 good(admin,'/ici/team/'+row['id']+'/scope','PUT',{'portfolio':portfolio,'slot':row['iciSlot'],'regionIds':regions or []})
for label,role,options in [('training','National Director',['Business','Executive Leadership','Service','Training','Nari Cell','National pro']),('rd','Regional Director',['Business','Executive Leadership','Service','Training','Nari Cell']),('eps','National Coordinator',['Mini & Mega Impact','EPS','Young Minds','Skill Proficiency','Social Media'])]:
 client=clients[role]
 if label=='training':configure('business','National pro')
 for portfolio in options:
  if label=='training' and portfolio=='National pro':configure('business','Business')
  configure(label,portfolio)
  rows=good(client,'/ici/reports?search=QA%20Access%20'+suffix)['items']
  kind='Leadership' if portfolio=='Executive Leadership' else portfolio
  wanted=(events.get(kind) or programs[portfolio])['id']
  check(role+' enforces '+portfolio+' portfolio across reports',wanted in {e['id'] for e in rows} and all(e['activityType']==kind or e['activityProgram']==portfolio for e in rows))
  # A caller-supplied portfolio filter must narrow, never replace assignment scope.
  wrong='Business' if portfolio!='Business' else 'Training'
  check(role+' cannot override '+portfolio+' through query parameters',good(client,'/ici/reports?search=QA%20Access%20'+suffix+'&portfolio='+wrong)['total']==0)
 configure(label,{'training':'Training','rd':'Business','eps':'EPS'}[label])

def propose(client,e):return good(client,'/events/'+e['id']+'/status','POST',{'action':'Submit','version':e['version']})
def gov(client,p,decision='Approve',comment='QA independent review',version=None):
 request=good(admin,'/governance/requests/'+p['approvalRequestId'])['request']
 return client.req('/governance/requests/'+request['id']+'/decisions','POST',{'decision':decision,'comment':comment,'version':version or request['version']})
proposal=propose(cp,events['Training'])
check('Wrong-region president cannot open a club event review',login(fixture['emails']['rp2'],'Regional President')[0].req('/governance/requests/'+proposal['approvalRequestId'])[0] in [403,404])
old=good(admin,'/governance/requests/'+proposal['approvalRequestId'])['request']['version']
check('First event stage accepts Regional President',gov(rp,proposal)[0]==200)
check('Event decisions reject stale versions',gov(nd,proposal,version=old)[0]==409)
check('Second event stage accepts matching National Director',gov(nd,proposal)[0]==200)
latest=good(cp,'/events/'+events['Training']['id'])['eventInfo']
published=good(cp,'/events/'+latest['id']+'/status','POST',{'action':'Publish','version':latest['version']})
check('Approved club event can actually publish',published['publication']=='Published' and published['approval']=='Approved')
reject=propose(cp,events['Business']);check('Event rejection requires a reason',gov(rp,reject,'Reject','')[0]==400)
check('Regional President can reject a club proposal',gov(rp,reject,'Reject','QA rejection for correction')[0]==200)
check('Rejected event cannot publish',cp.req('/events/'+events['Business']['id']+'/status','POST',{'action':'Publish','version':good(cp,'/events/'+events['Business']['id'])['eventInfo']['version']})[0]==409)
changes=propose(cp,events['Service']);check('Regional President can request event corrections',gov(rp,changes,'RequestChanges','QA add project details')[0]==200)
check('Pending event cannot bypass review and publish',cp.req('/events/'+events['Service']['id']+'/status','POST',{'action':'Publish','version':good(cp,'/events/'+events['Service']['id'])['eventInfo']['version']})[0]==409)

# Leave a valid pending club proposal and transfer for browser checks of review controls.
pending=propose(cp,events['Nari Cell'])
member={'id':sql("SELECT m.\"Id\" FROM \"Memberships\" m WHERE m.\"ClubId\"='"+c1+"' AND m.\"Status\"='Active' AND NOT EXISTS(SELECT 1 FROM \"MembershipTransfers\" t WHERE t.\"MembershipId\"=m.\"Id\" AND t.\"Status\" LIKE 'Pending%') ORDER BY m.\"Id\" LIMIT 1")}
membership=good(cp,'/members/'+member['id'])['membership']
t=good(cp,'/members/'+member['id']+'/transfers','POST',{'destinationClubId':c2,'categoryId':membership['categoryId'],'reason':'QA UI transfer authorization','version':membership['version']})
def transfer_row(client):return next(x for x in good(client,'/membership/transfers')['items'] if x['id']==t['id'])
check('Source RP sees an actionable transfer',transfer_row(rp).get('canReview') is True)
check('NP dashboard does not offer premature transfer review',transfer_row(np).get('canReview') is False)
check('NVP dashboard does not offer premature transfer review',transfer_row(nvp).get('canReview') is False)
check('Destination RP does not offer source-stage decisions',transfer_row(login(fixture['emails']['rp2'],'Regional President')[0]).get('canReview') is False)
check('NP cannot bypass regional review by cancelling another person\'s transfer',np.req('/membership/transfers/'+t['id']+'/cancel','POST',{'version':t['version']})[0]==403)
check('NVP cannot bypass regional review by cancelling another person\'s transfer',nvp.req('/membership/transfers/'+t['id']+'/cancel','POST',{'version':t['version']})[0]==403)
check('Pending event UI offers decisions only at the correct stage',next(r for r in good(rp,'/ici/event-reviews') if r['id']==pending['approvalRequestId'])['canReview'] is True and next(r for r in good(eb,'/ici/event-reviews') if r['id']==pending['approvalRequestId'])['canReview'] is False)
# The same person may hold two offices but must never approve both publication stages.
ndRole=next(r['id'] for r in good(admin,'/roles') if r['name']=='National Director')
team=good(admin,'/ici/team')['assignments']
if not any(r['email']==role_emails['Regional President'] and r['role']=='National Director' and r['portfolio']=='Nari Cell' for r in team):
 good(admin,'/ici/team','POST',{'email':role_emails['Regional President'],'roleId':ndRole,'orgId':nat,'portfolio':'Nari Cell'})
check('RP completes first stage while holding a second office',gov(rp,pending)[0]==200)
dual,_=login(role_emails['Regional President'],'National Director')
check('Event workspace hides duplicate approval for a person with two roles',next(r for r in good(dual,'/ici/event-reviews') if r['id']==pending['approvalRequestId'])['canReview'] is False)
check('Full governance proposal also hides duplicate-actor review',good(dual,'/governance/requests/'+pending['approvalRequestId'])['canDecide'] is False)
check('Second office cannot bypass distinct event reviewers',gov(dual,pending)[0]==403)
# Keep a Training proposal available for checking the RP-to-ND transition in the UI.
ui_event=event('Training');ui_proposal=propose(cp,ui_event)
pathlib.Path('.infra/ici-access-ui-event.json').write_text(json.dumps({'event':ui_event,'proposal':ui_proposal}))
pathlib.Path('.infra/ici-access-fixtures.json').write_text(json.dumps({'roles':role_emails,'events':events,'pending':pending,'transfer':t,'suffix':suffix},indent=2))
failed=[r['test'] for r in results if not r['passed']]
print(json.dumps({'passed':len(results)-len(failed),'failed':failed}),flush=True)
assert not failed,failed
