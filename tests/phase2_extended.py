"""PostgreSQL-backed quorum, frozen workflows, scheduled transfers and district IDOR."""
from support import Client,sql,PASSWORD
import uuid,json,pathlib,time,datetime
results=[]
def check(name,ok,data=None):
 print(('PASS ' if ok else 'FAIL ')+name);results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase2-extended.json').write_text(json.dumps(results,indent=2));assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,body=c.req(p,m,d);assert code==status,(p,code,body);return body
admin=Client();assert admin.login('admin@example.test')[0]==200
root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
cfg=good(admin,'/membership/config');cat=next(c for c in cfg['categories'] if c['code']=='REG');original=[{'name':s['name'],'roleIds':[r['roleId'] for r in s['roles']],'requiredApprovals':s['requiredApprovals']} for s in sorted(cfg['workflow']['stages'],key=lambda s:s['sequence'])]
roles={r['name']:r['id'] for r in cfg['roles']};club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');dest=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-008\'')
applicant=Client();email='extended-'+uuid.uuid4().hex+'@example.test'
good(applicant,'/auth/register','POST',{'name':'Extended Applicant '+uuid.uuid4().hex[:8],'email':email,'phone':'','password':PASSWORD,'confirmation':PASSWORD,'agreed':True},202)
mail=next(x for x in good(applicant,'/development/mail') if x['email']==email);good(applicant,'/auth/complete','POST',{'token':mail['link'].split('token=')[1].split('&')[0],'password':''});assert applicant.login(email)[0]==200
sec=Client();sec.login('secretary@example.test');sec.activate(next(w for w in good(sec,'/workspaces') if w['role']=='Club Secretary'))
pres=Client();pres.login('president@example.test');pres.activate(next(w for w in good(pres,'/workspaces') if w['role']=='Club President'))
try:
 flow=good(admin,'/membership/workflows','POST',{'stages':[{'name':'Two independent reviews','roleIds':[roles['Club Secretary'],roles['Club President']],'requiredApprovals':2}]})
 a=good(applicant,'/membership/applications','POST',{'clubId':club,'categoryId':cat['id'],'statement':'Quorum and frozen workflow integration test'})
 a=good(applicant,'/membership/applications/'+a['id']+'/submit','POST',{'version':a['version']})
 good(admin,'/membership/workflows','POST',{'stages':original})
 check('submitted application retains frozen workflow version',sql(f'SELECT "WorkflowId" FROM "MembershipApplications" WHERE "Id"=\'{a["id"]}\'')==flow['id'])
 a=good(sec,'/membership/applications/'+a['id']+'/decisions','POST',{'decision':'Approve','comment':'First independent review','version':a['version']})
 check('quorum does not activate after first approval',a['status']=='Under Review' and not good(applicant,'/membership/me')['memberships'])
 check('same officer cannot satisfy quorum twice',sec.req('/membership/applications/'+a['id']+'/decisions','POST',{'decision':'Approve','comment':'Duplicate review','version':a['version']})[0]==409)
 a=good(pres,'/membership/applications/'+a['id']+'/decisions','POST',{'decision':'Approve','comment':'Second independent review','version':a['version']})
 check('two independent approvers satisfy frozen quorum',a['status']=='Approved')
 m=good(applicant,'/membership/me')['memberships'][0]
 policy=dict(cfg['policy']);policy['financialClearanceRequired']=True;good(admin,'/membership/policy','PUT',policy,204)
 effective=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(seconds=20)).isoformat()
 t=good(applicant,'/members/'+m['id']+'/transfers','POST',{'destinationClubId':dest,'categoryId':cat['id'],'reason':'Scheduled administrative clearance test','version':m['version'],'effectiveAt':effective})
 t=good(admin,'/membership/transfers/'+t['id']+'/decisions','POST',{'decision':'Approve','comment':'Source approved','version':t['version']})
 check('outstanding administrative clearance blocks final transfer',admin.req('/membership/transfers/'+t['id']+'/decisions','POST',{'decision':'Approve','comment':'Destination approval','version':t['version']})[0]==409)
 good(admin,'/membership/transfers/'+t['id']+'/clearance','POST',{'hold':False,'reason':'Officer checked administrative clearance; no payment asserted','version':t['version']},204);t['version']+=1
 t=good(admin,'/membership/transfers/'+t['id']+'/decisions','POST',{'decision':'Approve','comment':'Destination approval','version':t['version']})
 check('future transfer schedules without early mutation',t['status']=='Scheduled' and good(applicant,'/members/'+m['id'])['membership']['status']=='Active')
 deadline=time.monotonic()+35
 while time.monotonic()<deadline:
  state=sql(f'SELECT "Status" FROM "MembershipTransfers" WHERE "Id"=\'{t["id"]}\'')
  if state=='Completed':break
  time.sleep(.4)
 check('durable worker completes transfer at effective time',state=='Completed')
finally:
 good(admin,'/membership/workflows','POST',{'stages':original});good(admin,'/membership/policy','PUT',cfg['policy'],204)
# Create another valid district/zone/club using existing hierarchy constraints.
suffix=uuid.uuid4().hex[:8];district=str(uuid.uuid4());zone=str(uuid.uuid4());outside=str(uuid.uuid4())
for seed,new,parent in [('GC-004',district,None),('GC-005',zone,district),('GC-006',outside,zone)]:
 overrides={'Id':new,'Code':'EXT-'+new[:8],'Name':'Extended '+seed+' '+suffix}
 if parent:overrides['ParentId']=parent
 sql(f'''INSERT INTO "Orgs" SELECT (jsonb_populate_record(NULL::"Orgs",to_jsonb(o)||'{json.dumps(overrides)}'::jsonb)).* FROM "Orgs" o WHERE "Code"='{seed}' ''')
member=good(admin,'/members','POST',{'clubId':outside,'categoryId':cat['id'],'person':{'legalName':'District Test '+suffix,'displayName':'District Test '+suffix}})
role=good(admin,'/roles','POST',{'name':'District privacy test '+suffix,'permissions':['members.view','directory.view','members.manage','members.export']})
district1=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-004\'')
good(admin,'/assignments','POST',{'email':'manager@example.test','roleId':role['id'],'orgId':district1},204)
officer=Client();officer.login('manager@example.test');officer.activate(next(w for w in good(officer,'/workspaces') if w['role']==role['name']))
check('cross-district private profile denied',officer.req('/members/'+member['id'])[0]==404)
check('cross-district member list filter cannot widen scope',not good(officer,'/members?clubId='+outside)['items'])
check('cross-district administrative creation denied',officer.req('/members','POST',{'clubId':outside,'categoryId':cat['id'],'person':{'legalName':'Denied District Person','displayName':'Denied District Person'}})[0]==403)
check('district officer sees authorized descendant members',good(officer,'/members')['total']>0)
print(len(results),'extended checks passed')

