"""Legacy-route, historical confidentiality, calendar and background-authority regressions."""
from support import Client,sql
import uuid,json,pathlib,time,datetime
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name);pathlib.Path('test-results/phase3-security.json').write_text(json.dumps(results,indent=2));assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,body=c.req(p,m,d);assert code==status,(p,code,body);return body
def at(seconds=0):return (datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(seconds=seconds)).isoformat()
tag=uuid.uuid4().hex[:8];admin=Client();admin.login('admin@example.test');root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
review=Client();review.login('secretary@example.test');review.activate(next(w for w in good(review,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global')))
member=Client();member.login('member@example.test');club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');zone=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-005\'');cfg=good(admin,'/governance/options');level=next(l for l in cfg['levels'] if l['isClub']);person=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')')
def approve(r):
 r=good(admin,'/governance/requests/'+r['id']+'/submit','POST',{'version':r['version']});return good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent security regression review','version':r['version']})
legacy=good(admin,'/organizations','POST',{'name':'Legacy approval club '+tag,'code':'LEG-'+tag,'levelId':level['id'],'parentId':zone,'status':'Active'})
check('legacy club creation cannot bypass governance',legacy['status']=='Pending')
check('legacy direct activation blocked',admin.req('/organizations/'+legacy['id'],'PUT',{**legacy,'status':'Active'})[0]==409)
request=next(r for r in good(admin,'/governance/requests')['items'] if r['orgId']==legacy['id']);approve(request);check('legacy proposal uses same approval engine',good(admin,'/clubs/'+legacy['id'])['club']['status']=='Active')
check('legacy workflow kinds migrated',sql('SELECT count(*) FROM "MembershipWorkflows" WHERE "Kind"=\'\'')=='0')
check('legacy fiscal defaults migrated',sql('SELECT count(*) FROM "Orgs" WHERE "FiscalStartMonth"=0 OR "BrandingColor"=\'\'')=='0')
term=good(admin,'/leadership-terms','POST',{'orgId':root['orgId'],'name':'Custom shared year '+tag,'calendar':'Fiscal','startsAt':at(-86400),'endsAt':at(86400),'timeZone':'Asia/Kolkata'})
check('ancestor leadership calendar applies to child organization',any(t['id']==term['id'] for t in good(admin,'/leadership-terms?applicableToOrgId='+club)['items']))
role=good(admin,'/roles','POST',{'name':'Historical document reader '+tag,'permissions':['governance.documents','leadership.view']})
position=good(admin,'/officer-positions','POST',{'code':'HIST-'+tag,'title':'Records officer '+tag,'levelId':level['id'],'grantRoleId':role['id'],'requireActiveMembership':True,'maximumHolders':1,'defaultMonths':12})
base={'orgId':club,'personId':person,'positionId':position['id'],'startsAt':at(-10),'endsAt':at(3600),'termId':term['id']}
check('appointment cannot exceed selected calendar',admin.req('/officer-appointments','POST',{**base,'endsAt':at(172800)})[0]==400)
check('appointment must satisfy membership eligibility at start',admin.req('/officer-appointments','POST',{**base,'termId':None,'startsAt':'2020-01-01T00:00:00Z','endsAt':at(3600)})[0]==400)
pdf=b'%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF'
status,private=admin.upload('/governance/documents','historical.pdf',pdf,{'orgId':club,'title':'Private historical document','kind':'Policy','shareWithSuccessors':'false'},'application/pdf');assert status==200
status,shared=admin.upload('/governance/documents','shared.pdf',pdf,{'orgId':club,'title':'Explicit successor document','kind':'Policy','shareWithSuccessors':'true'},'application/pdf');assert status==200
sql(f'UPDATE "GovernanceDocuments" SET "At"=now()-interval \'1 day\' WHERE "Id" IN (\'{private["id"]}\',\'{shared["id"]}\')')
resolution=good(admin,'/governance/resolutions','POST',{'orgId':club,'number':'CONF-'+tag,'title':'Historical confidential resolution','subject':'Governance','text':'Restricted historical resolution content','date':at(-86400),'effectiveAt':at(-86400),'documentId':private['id']});approve(resolution['request']);sql(f'UPDATE "GovernanceRequests" SET "CreatedAt"=now()-interval \'1 day\' WHERE "Id"=\'{resolution["request"]["id"]}\'')
appointment=good(admin,'/officer-appointments','POST',base);approve(appointment['request']);workspace=next(w for w in good(member,'/workspaces') if w['role']==role['name']);member.activate(workspace)
check('term-derived document role cannot read historical private file',member.request('/governance/documents/'+private['id'])[0]==404)
check('explicit successor sharing permits appropriate historical file',member.request('/governance/documents/'+shared['id'])[0]==200)
check('historical confidential resolution excluded from successor list',not any(r['id']==resolution['id'] for r in good(member,'/governance/resolutions')['items']))
check('historical request payload cannot bypass resolution privacy',member.req('/governance/requests/'+resolution['request']['id'])[0]==404)
check('historical metadata absent from successor document list',not any(d['id']==private['id'] for d in good(member,'/governance/documents')['items']))
# Immutable historical labels survive later position configuration changes.
sql(f'UPDATE "OfficerAppointments" SET "EndsAt"=now()-interval \'1 second\' WHERE "Id"=\'{appointment["id"]}\'')
deadline=time.monotonic()+10
while time.monotonic()<deadline:
 if sql(f'SELECT "Status" FROM "OfficerAppointments" WHERE "Id"=\'{appointment["id"]}\'')=='Completed':break
 time.sleep(.3)
newposition=good(admin,'/officer-positions/'+position['id'],'PUT',{'code':position['code'],'title':'Renamed records office '+tag,'levelId':level['id'],'grantRoleId':role['id'],'defaultMonths':12,'maximumHolders':1,'requireActiveMembership':True,'version':position['version']})
check('position renaming preserves historical title snapshot',good(admin,'/officer-appointments/'+appointment['id'])['appointment']['positionTitle']==position['title'])
member.activate(next(w for w in good(member,'/workspaces') if w['role']=='Club Member' and w['orgId']==club))
# A queued import loses permission when its appointing term expires, even before grant cleanup.
importrole=good(admin,'/roles','POST',{'name':'Term importer '+tag,'permissions':['members.import','members.contacts.view','members.view']});ip=good(admin,'/officer-positions','POST',{'code':'IMP-'+tag,'title':'Term importing officer '+tag,'levelId':level['id'],'grantRoleId':importrole['id'],'maximumHolders':1,'defaultMonths':12,'requireActiveMembership':True});office=good(admin,'/officer-appointments','POST',{**base,'positionId':ip['id'],'termId':None});approve(office['request']);member.activate(next(w for w in good(member,'/workspaces') if w['role']==importrole['name']))
category=next(c for c in good(admin,'/membership/config')['categories'] if c['code']=='REG');name='Expiry import '+tag
status,job=member.upload('/membership/imports','authority.csv',('Name\n'+name+'\n').encode(),{'clubId':club,'categoryId':category['id'],'requestKey':uuid.uuid4().hex});assert status==200
good(member,'/membership/imports/'+job['id']+'/mapping','POST',{'fields':{'name':'Name'}},202)
deadline=time.monotonic()+12
while time.monotonic()<deadline:
 state=good(member,'/membership/imports/'+job['id'])['status']
 if state=='Ready':break
 time.sleep(.3)
assert state=='Ready'
good(member,'/membership/imports/'+job['id']+'/commit','POST',{},202)
sql(f'UPDATE "OfficerAppointments" SET "EndsAt"=now()-interval \'1 second\' WHERE "Id"=\'{office["id"]}\'')
deadline=time.monotonic()+15
while time.monotonic()<deadline:
 state=good(admin,'/membership/imports/'+job['id'])['status']
 if state=='Failed':break
 time.sleep(.3)
check('background import revalidates effective appointment authority',state=='Failed')
check('expired-term import remains atomic',sql(f'SELECT count(*) FROM "Persons" WHERE "DisplayName"=\'{name}\'')=='0')
# District permissions and aggregates use the same recursive hierarchy as all other modules.
district=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-004\'');dr=good(admin,'/roles','POST',{'name':'District governance reader '+tag,'permissions':['clubs.view','leadership.view','governance.reports','committees.view']});good(admin,'/assignments','POST',{'email':'manager@example.test','roleId':dr['id'],'orgId':district},204)
manager=Client();manager.login('manager@example.test');manager.activate(next(w for w in good(manager,'/workspaces') if w['role']==dr['name']));d=good(manager,'/administration/dashboard');expected=int(sql(f'''WITH RECURSIVE tree AS(SELECT "Id" FROM "Orgs" WHERE "Id"='{district}' UNION ALL SELECT o."Id" FROM "Orgs" o JOIN tree t ON o."ParentId"=t."Id") SELECT count(*) FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE o."Id" IN(SELECT "Id" FROM tree) AND l."IsClub"'''));check('district dashboard aggregation matches recursive SQL truth',d['clubs']==expected)
outside=sql('SELECT o."Id" FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."IsClub" AND o."Code" LIKE \'EXT-%\' LIMIT 1');check('cross-district club record denied',manager.req('/clubs/'+outside)[0]==404)
national=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-002\'');good(admin,'/assignments','POST',{'email':'manager@example.test','roleId':dr['id'],'orgId':national},204)
manager.activate(next(w for w in good(manager,'/workspaces') if w['role']==dr['name'] and w['orgId']==national));n=good(manager,'/administration/dashboard')
expected=int(sql(f'''WITH RECURSIVE tree AS(SELECT "Id" FROM "Orgs" WHERE "Id"='{national}' UNION ALL SELECT o."Id" FROM "Orgs" o JOIN tree t ON o."ParentId"=t."Id") SELECT count(*) FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE o."Id" IN(SELECT "Id" FROM tree) AND l."IsClub"'''))
check('national dashboard aggregation matches recursive SQL truth',n['clubs']==expected)
member.activate(next(w for w in good(member,'/workspaces') if w['role']=='Club Member' and w['orgId']==club))
check('ordinary member cannot configure governance workflows',member.req('/governance/workflows','POST',{'stages':[]})[0]==403)
print(len(results),'governance security checks passed')
