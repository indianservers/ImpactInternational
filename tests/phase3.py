"""Governance integration tests against actual PostgreSQL, effective access and HTTP."""
from support import Client,sql,PSQL
import uuid,json,pathlib,time,datetime,concurrent.futures,subprocess
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,'' if ok else str(data)[:700]);pathlib.Path('test-results/phase3.json').write_text(json.dumps(results,indent=2));assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,body=c.req(p,m,d);check(m+' '+p.split('?')[0],code==status,body);return body
def when(seconds=0):return (datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(seconds=seconds)).isoformat()
suffix=uuid.uuid4().hex[:8];admin=Client();check('administrator login',admin.login('admin@example.test')[0]==200)
ws=good(admin,'/workspaces');root=next(w for w in ws if w['role']=='Association Super Admin' and w['association'].startswith('Global'));other=next(w for w in ws if w['association']=='Civic Futures Network');admin.activate(root)
cfg=good(admin,'/governance/options');club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');dest=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-008\'');parent=sql('SELECT "ParentId" FROM "Orgs" WHERE "Id"=\''+club+'\'');level=next(l for l in cfg['levels'] if l['isClub']);candidate=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')')
# A separate authorized reviewer, never the requester/subject.
super_role=next(r for r in cfg['roles'] if r['name']=='Association Super Admin')
review=Client();review.login('secretary@example.test');review_ws=good(review,'/workspaces')
if not any(w['role']=='Association Super Admin' and w['orgId']==root['orgId'] for w in review_ws):good(admin,'/assignments','POST',{'email':'secretary@example.test','roleId':super_role['id'],'orgId':root['orgId']},204)
else:check('existing independent reviewer authority reused',True)
review.activate(next(w for w in review.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global')))
member=Client();member.login('member@example.test');member.activate(next(w for w in good(member,'/workspaces') if w['role']=='Club Member' and w['orgId']==club))
pres=Client();pres.login('president@example.test');pres.activate(next(w for w in good(pres,'/workspaces') if w['role']=='Club President'))
def submit(request,c=admin):return good(c,'/governance/requests/'+request['id']+'/submit','POST',{'version':request['version']})
def decide(request,decision='Approve',c=review):return good(c,'/governance/requests/'+request['id']+'/decisions','POST',{'decision':decision,'comment':'Independent governance review '+suffix,'version':request['version']})
def approve(request):return decide(submit(request))
newclub={'name':'Governance Club '+suffix,'code':'GOV-'+suffix,'parentId':parent,'levelId':level['id'],'country':'India','city':'Test City','email':'official@example.test','meetingSchedule':'First Monday','meetingVenue':'Community hall'}
r=good(admin,'/clubs','POST',newclub);check('club proposal does not bypass approval',sql(f'SELECT count(*) FROM "Orgs" WHERE "Code"=\'{newclub["code"]}\'')=='0');r=submit(r);check('self approval denied',admin.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'','version':r['version']})[0]==403);r=decide(r);new_id=r['resultId'];check('approved club creation persisted',sql(f'SELECT "Status" FROM "Orgs" WHERE "Id"=\'{new_id}\'')=='Active')
check('duplicate club code rejected',admin.req('/clubs','POST',newclub)[0]==409)
check('invalid parent type rejected',admin.req('/clubs','POST',{**newclub,'code':'BAD-'+suffix,'parentId':root['orgId']})[0]==400)
check('cross-tenant parent rejected',admin.req('/clubs','POST',{**newclub,'code':'CROSS-'+suffix,'parentId':other['orgId']})[0] in (403,404))
c=good(admin,'/clubs/'+new_id)['club'];change={**newclub,'name':'Updated Governance Club '+suffix,'version':c['version']};r=good(admin,'/clubs/'+new_id,'PUT',change);approve(r);check('club profile changes require independent decision',good(admin,'/clubs/'+new_id)['club']['name']==change['name']);check('club history retains before/after',good(admin,'/clubs/'+new_id+'/history')['total']>=2)
for status in ['Suspended','Active','Inactive','Closed']:
 c=good(admin,'/clubs/'+new_id)['club'];r=good(admin,'/clubs/'+new_id+'/status','POST',{'status':status,'reason':'Lifecycle integration test','version':c['version']});approve(r);check('club lifecycle '+status,good(admin,'/clubs/'+new_id)['club']['status']==status)
check('ordinary member club administration denied',member.req('/clubs')[0]==403)
role=good(admin,'/roles','POST',{'name':'Governance Term Reader '+suffix,'permissions':['organization.view','clubs.view','leadership.view','governance.reports']})
position={'code':'P3-'+suffix,'title':'Service Convenor '+suffix,'levelId':level['id'],'maximumHolders':1,'defaultMonths':12,'grantRoleId':role['id'],'mandatory':True,'requireActiveMembership':True,'approvalRequired':True}
p=good(admin,'/officer-positions','POST',position)
check('position grants do not escalate creator authority',pres.req('/officer-positions','POST',position)[0]==403)
base={'orgId':club,'personId':candidate,'positionId':p['id'],'startsAt':when(-60),'endsAt':when(3600),'method':'Appointed','notes':'Verified integration appointment'}
admin_person=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'admin@example.test\')')
check('unauthorized self-appointment denied',admin.req('/officer-appointments','POST',{**base,'personId':admin_person})[0]==403)
app=good(admin,'/officer-appointments','POST',base);check('proposed appointment has no authority',app['status']=='Proposed' and not any(w['role']==role['name'] for w in member.req('/workspaces')[1]));req=app['request'];req=submit(req)
# Actual same-version concurrent HTTP decisions have one winner.
review2=Client();review2.login('secretary@example.test');review2.activate(next(w for w in review2.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global')))
def vote(c):return c.req('/governance/requests/'+req['id']+'/decisions','POST',{'decision':'Approve','comment':'Concurrent independent review','version':req['version']})[0]
with concurrent.futures.ThreadPoolExecutor(2) as pool:codes=list(pool.map(vote,[review,review2]))
check('concurrent governance decisions have one winner',sorted(codes)==[200,409],codes)
record=good(admin,'/officer-appointments/'+app['id']);check('appointment active after approval',record['appointment']['status']=='Active');check('leadership history preserved',len(record['history'])>=3)
office_ws=next(w for w in member.req('/workspaces')[1] if w['role']==role['name']);member.activate(office_ws);check('explicit term role grants scoped access',member.req('/clubs')[0]==200)
check('overlapping office capacity rejected',admin.req('/officer-appointments','POST',base)[0]==409)
check('cross-club office cannot widen private scope',member.req('/clubs/'+dest)[0]==404)
check('cross-tenant governance request denied',member.req('/clubs/'+other['orgId'])[0]==404)
# Hold the worker's tenant lock while proving request-time expiry on an unprocessed Active row.
sql(f'UPDATE "OfficerAppointments" SET "EndsAt"=now()-interval \'1 second\' WHERE "Id"=\'{app["id"]}\'')
lock=subprocess.Popen([PSQL,'-h','127.0.0.1','-p','55432','-U','clubs','-d','clubs_test','-X','-At','-c',f'BEGIN; SELECT pg_advisory_xact_lock(hashtext(\'{root["tenantId"]}\')); SELECT pg_sleep(8); COMMIT;'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
try:check('effective dates deny expired administrative authority',member.req('/clubs')[0]==403)
finally:lock.terminate();lock.wait()
deadline=time.monotonic()+12
while time.monotonic()<deadline:
 state=sql(f'SELECT "Status" FROM "OfficerAppointments" WHERE "Id"=\'{app["id"]}\'')
 if state=='Completed':break
 time.sleep(.3)
check('term worker completes and preserves expired appointment',state=='Completed')
check('only appointment-sourced grant removed',not any(w['role']==role['name'] for w in member.req('/workspaces')[1]) and any(w['role']=='Club Member' and w['orgId']==club for w in member.req('/workspaces')[1]))
# Multiple permitted officeholders; title-only appointments never grant access by title.
p2=good(admin,'/officer-positions','POST',{**position,'code':'MANY-'+suffix,'title':'Advisory director '+suffix,'maximumHolders':2,'grantRoleId':None,'mandatory':False})
candidates=good(admin,'/leadership/eligible?orgId='+club)['items'];other_person=next(x['id'] for x in candidates if x['id'] not in (candidate,admin_person))
for person in [candidate,other_person]:
 a=good(admin,'/officer-appointments','POST',{**base,'personId':person,'positionId':p2['id']});approve(a['request'])
check('two configured concurrent officeholders allowed',int(sql(f'SELECT count(*) FROM "OfficerAppointments" WHERE "PositionId"=\'{p2["id"]}\' AND "Status"=\'Active\''))==2)
rejected=good(admin,'/officer-appointments','POST',{**base,'positionId':p['id']});decide(submit(rejected['request']),'Reject');check('appointment rejection grants no authority',good(admin,'/officer-appointments/'+rejected['id'])['appointment']['status']=='Cancelled')
future=good(admin,'/officer-appointments','POST',{**base,'positionId':p['id'],'startsAt':when(9),'endsAt':when(3600)});approve(future['request']);check('future appointment is scheduled',good(admin,'/officer-appointments/'+future['id'])['appointment']['status']=='Scheduled');check('future title grants no early authority',not any(w['role']==role['name'] for w in member.req('/workspaces')[1]))
deadline=time.monotonic()+18
while time.monotonic()<deadline:
 if any(w['role']==role['name'] for w in member.req('/workspaces')[1]):break
 time.sleep(.4)
check('scheduled appointment activates through durable worker',any(w['role']==role['name'] for w in member.req('/workspaces')[1]))
# Committee scope is independent of organization access.
com=good(admin,'/committees','POST',{'orgId':club,'name':'Community Outreach '+suffix,'code':'COM-'+suffix,'purpose':'Verified committee operations','startsAt':when(-100),'endsAt':when(86400)});approve(com['request']);cid=com['id'];roles=good(admin,'/committees/'+cid+'/roles');chair=next(r for r in roles if r['canManage']);ordinary=next(r for r in roles if not r['canManage'])
cm={'personId':candidate,'roleId':chair['id'],'startsAt':when(-60),'endsAt':when(3600),'responsibilities':'Coordinate service planning'};chair_req=good(admin,'/committees/'+cid+'/chair','POST',cm);approve(chair_req)
member.activate(next(w for w in member.req('/workspaces')[1] if w['role']=='Club Member' and w['orgId']==club))
check('committee chair can manage own committee',good(member,'/committees/'+cid)['canManage'])
assigned=good(member,'/committees/'+cid+'/members','POST',{**cm,'personId':other_person,'roleId':ordinary['id']});check('chair can assign ordinary committee member',assigned['status']=='Assigned')
check('chair cannot grant committee leadership directly',member.req('/committees/'+cid+'/members','POST',{**cm,'personId':admin_person})[0]==409)
outside=good(admin,'/committees','POST',{'orgId':dest,'name':'Other Committee '+suffix,'code':'OTHER-'+suffix,'purpose':'Scope isolation test','startsAt':when(-60),'endsAt':when(86400)});approve(outside['request']);check('chair cannot manage another committee',member.req('/committees/'+outside['id'])[0]==404)
cmember=next(m for m in good(member,'/committees/'+cid)['members'] if m['personId']==other_person);good(member,f'/committees/{cid}/members/{cmember["id"]}/remove','POST',{'reason':'Committee duty completed','version':cmember['version']});check('committee removal preserves history',any(m['id']==cmember['id'] and m['status']=='Removed' for m in good(member,'/committees/'+cid)['members']))
# Actual secure documents and independently approved resolution.
pdf=b'%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF'
status,document=admin.upload('/governance/documents','resolution.pdf',pdf,{'orgId':club,'title':'Governance policy '+suffix,'kind':'Policy','shareWithSuccessors':'false'},'application/pdf');check('secure governance document upload',status==200,document)
check('anonymous governance document access denied',Client().request('/governance/documents/'+document['id'])[0]==401)
check('ordinary member cannot access organizational document',member.request('/governance/documents/'+document['id'])[0]==403)
check('authorized governance document download',admin.request('/governance/documents/'+document['id'])[0]==200)
check('unsafe governance file rejected',admin.upload('/governance/documents','bad.html',b'<script>x</script>',{'orgId':club,'title':'Bad','kind':'Policy','shareWithSuccessors':'false'},'text/html')[0]==400)
resolution=good(admin,'/governance/resolutions','POST',{'orgId':club,'number':'RES-'+suffix,'title':'Service policy','subject':'Club governance','text':'The board records an integration-tested governance policy.','date':when(),'effectiveAt':when()});approve(resolution['request']);check('resolution approval persisted',any(r['id']==resolution['id'] and r['status']=='Approved' for r in good(admin,'/governance/resolutions')['items']))
health=good(admin,'/clubs/'+club+'/health');weights=health['configuration'];expected=round(health['leadershipCoverage']/100*weights['leadershipWeight']+min(health['activeMembers']/weights['minimumActiveMembers'],1)*weights['membershipWeight']+weights['governanceWeight']/(1+health['pendingGovernance'])+health['dataFieldsComplete']/health['dataFieldsTotal']*weights['dataWeight'],1);check('club health uses documented weighted formula',abs(health['score']-expected)<.11,(health,expected))
dash=good(admin,'/administration/dashboard');expected=int(sql(f'SELECT count(DISTINCT "PersonId") FROM "Memberships" WHERE "TenantId"=\'{root["tenantId"]}\' AND "Status"=\'Active\' AND ("EndsAt" IS NULL OR "EndsAt">now())'));check('national aggregate matches source memberships',dash['activeMembers']==expected,(dash['activeMembers'],expected))
scoped=good(pres,'/administration/dashboard');check('club officer dashboard stays within club scope',scoped['clubs']==1 and scoped['activeMembers']<dash['activeMembers'])
check('cross-tenant position reference denied',admin.req('/officer-positions','POST',{**position,'code':'CROSS-P-'+suffix,'levelId':sql('SELECT "LevelId" FROM "Orgs" WHERE "Id"=\''+other['orgId']+'\'')})[0]==400)
check('governance migration recorded',sql('SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE \'%GovernanceFoundation\'')=='1')
print(len(results),'Phase 3 checks passed')
