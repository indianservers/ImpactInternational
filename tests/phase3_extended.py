"""Handover, restructuring, revisions, quorum and delegation integration cases."""
from support import Client,sql
import uuid,datetime,time,json,pathlib
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name);pathlib.Path('test-results/phase3-extended.json').write_text(json.dumps(results,indent=2));assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,body=c.req(p,m,d);assert code==status,(p,code,body);return body
def at(seconds=0):return (datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(seconds=seconds)).isoformat()
tag=uuid.uuid4().hex[:8];admin=Client();admin.login('admin@example.test');root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
review=Client();review.login('secretary@example.test');review.activate(next(w for w in good(review,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global')))
pres=Client();pres.login('president@example.test');pres.activate(next(w for w in good(pres,'/workspaces') if w['role']=='Club President'))
cfg=good(admin,'/governance/options');club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');zone=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-005\'');level=next(l for l in cfg['levels'] if l['isClub'])
def submit(r,c=admin):return good(c,'/governance/requests/'+r['id']+'/submit','POST',{'version':r['version']})
def decision(r,choice='Approve',c=review):return good(c,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':choice,'comment':'Extended independent review','version':r['version']})
def approved(r):return decision(submit(r))
position=good(admin,'/officer-positions','POST',{'code':'HAND-'+tag,'title':'Transition convenor '+tag,'levelId':level['id'],'maximumHolders':1,'defaultMonths':12,'requireActiveMembership':True,'approvalRequired':True})
maya=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');arjun=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'president@example.test\')')
start=at(-100);boundary=at(30);end=at(86400)
out=good(admin,'/officer-appointments','POST',{'orgId':club,'personId':maya,'positionId':position['id'],'startsAt':start,'endsAt':boundary});approved(out['request'])
incoming=good(admin,'/officer-appointments','POST',{'orgId':club,'personId':arjun,'positionId':position['id'],'startsAt':boundary,'endsAt':end});approved(incoming['request'])
handover=good(admin,'/leadership-handovers','POST',{'outgoingAppointmentId':out['id'],'incomingAppointmentId':incoming['id'],'notes':'Outstanding actions and succession evidence','items':['Review pending actions','Review shared operating policies']});hid=handover['id']
check('handover captures actual administrative snapshot','activeMembers' in json.loads(good(admin,'/leadership-handovers/'+hid)['handover']['snapshot']))
check('incomplete handover cannot be accepted',pres.req('/leadership-handovers/'+hid+'/accept','POST',{'version':handover['version']})[0]==409)
data=good(admin,'/leadership-handovers/'+hid)
for item in data['items']:
 handover=good(admin,'/leadership-handovers/'+hid+'/items/'+item['id'],'PUT',{'completed':True,'notes':'Reviewed actual records','version':handover['version']})
check('outgoing/requester cannot impersonate incoming acceptance',admin.req('/leadership-handovers/'+hid+'/accept','POST',{'version':handover['version']})[0]==403)
handover=good(pres,'/leadership-handovers/'+hid+'/accept','POST',{'version':handover['version']});check('incoming officer accepts completed checklist',handover['version']>1)
req=good(admin,'/leadership-handovers/'+hid+'/submit','POST',{'version':handover['version']});req=submit(req)
check('incoming officer cannot approve own handover',pres.req('/governance/requests/'+req['id']+'/decisions','POST',{'decision':'Approve','comment':'','version':req['version']})[0]==403)
decision(req);check('independent review approves controlled handover',good(admin,'/leadership-handovers/'+hid)['handover']['status']=='Approved')
deadline=time.monotonic()+40
while time.monotonic()<deadline:
 state=good(admin,'/leadership-handovers/'+hid)['handover']['status']
 if state=='Archived':break
 time.sleep(.4)
check('handover archives after successor term takes effect',state=='Archived')
check('historical outgoing appointment remains completed',good(admin,'/officer-appointments/'+out['id'])['appointment']['status']=='Completed')
# Request-changes revision preserves old decisions and freezes the submitted workflow.
data={'name':'Revision Club '+tag,'code':'REV-'+tag,'parentId':zone,'levelId':level['id']}
r=good(admin,'/clubs','POST',data);r=submit(r);r=decision(r,'RequestChanges');request=good(admin,'/governance/requests/'+r['id'])['request'];data['description']='Clarified operating purpose'
new=good(admin,'/governance/requests/'+r['id'],'PUT',{'payload':json.dumps(data),'version':request['version']});r=submit(new);r=decision(r);created=r['resultId'];record=good(admin,'/governance/requests/'+r['id']);check('governance revisions retain earlier decisions',len(record['decisions'])==2 and record['request']['revision']==2)
# New valid zone under the same district; only controlled restructuring may move a club.
newzone=str(uuid.uuid4());sql(f'''INSERT INTO "Orgs" SELECT (jsonb_populate_record(NULL::"Orgs",to_jsonb(o)||jsonb_build_object('Id','{newzone}','Name','Restructuring zone {tag}','Code','ZONE-{tag}'))).* FROM "Orgs" o WHERE "Id"='{zone}' ''')
clubdata=good(admin,'/clubs/'+created)['club'];bad={**data,'parentId':newzone,'version':clubdata['version']}
check('profile update cannot bypass restructuring authority',admin.req('/clubs/'+created,'PUT',bad)[0]==400)
r=good(admin,'/organizations/'+created+'/restructure','POST',{'parentId':newzone,'reason':'Authorized district boundary adjustment','version':clubdata['version']});approved(r)
check('approved restructuring changes authoritative hierarchy',good(admin,'/clubs/'+created)['club']['parentId']==newzone)
check('club history records parent transition',any(json.loads(x['after'])['ParentId']==newzone for x in good(admin,'/clubs/'+created+'/history')['items']))
# Two-person quorum with explicit delegation: one principal cannot vote twice.
original=[{'name':st['name'],'roleIds':[r['roleId'] for r in st['roles']],'requiredApprovals':st['requiredApprovals']} for st in sorted(cfg['workflow']['stages'],key=lambda st:st['sequence'])]
superrole=next(r for r in cfg['roles'] if r['name']=='Association Super Admin')
try:
 good(admin,'/governance/workflows','POST',{'stages':[{'name':'Two independent authorities','roleIds':[superrole['id']],'requiredApprovals':2}],'allowDelegation':True})
 # Secretary and president have explicitly granted root reviewer workspaces; roles alone never imply scope.
 existing=good(pres,'/workspaces')
 if not any(w['role']=='Association Super Admin' and w['orgId']==root['orgId'] for w in existing):good(admin,'/assignments','POST',{'email':'president@example.test','roleId':superrole['id'],'orgId':root['orgId']},204)
 pres.activate(next(w for w in good(pres,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global')))
 draft=good(admin,'/governance/resolutions','POST',{'orgId':club,'number':'QUORUM-'+tag,'title':'Independent quorum','subject':'Governance review','text':'Two principals must review this decision.','date':at(),'effectiveAt':at()});r=submit(draft['request'])
 r=decision(r);check('one approval cannot satisfy two-person governance quorum',r['status']=='Under Review')
 # Delegate has the same effective scope, but cannot reuse an already-counted principal.
 pres_id=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'president@example.test\'');delegation=good(review,'/governance/delegations','POST',{'delegateId':pres_id,'startsAt':at(-10),'endsAt':at(3600)})
 check('delegation cannot double count the same principal',pres.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Delegated duplicate principal','version':r['version'],'delegationId':delegation['id']})[0]==409)
 r=decision(r,c=pres);check('two distinct principals complete governance quorum',r['status']=='Approved')
 good(review,'/governance/delegations/'+delegation['id']+'/revoke','POST',{},204)
finally:good(admin,'/governance/workflows','POST',{'stages':original,'allowDelegation':False})
print(len(results),'extended Phase 3 checks passed')
