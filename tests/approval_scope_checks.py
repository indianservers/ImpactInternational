import os,sys,json,uuid,datetime,pathlib
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5098'
os.environ['CLUBS_TEST_DATABASE']='clubs_approval_scope_20261114'
sys.path.insert(0,'tests');from support import Client,sql
checks=[]
def check(name,ok,detail=None):
 checks.append(dict(name=name,passed=bool(ok),detail=detail))
 if not ok: print('FAIL',name,detail,flush=True)
def req(c,path,method='GET',data=None,expected=200):
 s,d=c.req(path,method,data);check(method+' '+path,s==expected, None if s==expected else dict(status=s,data=d));assert s==expected,(path,s,d);return d
def login(email,role=None):
 c=Client();req(c,'/auth/login','POST',{'email':email,'password':__import__('support').PASSWORD});ws=req(c,'/workspaces');w=next(x for x in ws if role is None or x['role']==role);req(c,'/workspaces/'+w['id']+'/activate','POST',{},204);return c,w
admin,aw=login('saisatish@indianservers.com')
orgs=json.loads(sql('SELECT json_agg(o) FROM "Orgs" o'))
bycode={o['Code']:o for o in orgs}
clubs=[bycode[x] for x in ['DEMO-R01-D01-Z01-C01','DEMO-R01-D01-Z01-C02','DEMO-R01-D01-Z02-C01','DEMO-R01-D02-Z01-C01','DEMO-R02-D01-Z01-C01']]
byid={o['Id']:o for o in orgs}
def within(org,root):
 while org:
  if org['Id']==root:return True
  org=byid.get(org['ParentId'])
 return False
now=datetime.datetime.now(datetime.timezone.utc)
positions={}
def appointment(club):
 if club['LevelId'] not in positions:
  positions[club['LevelId']]=req(admin,'/officer-positions','POST',dict(code='SCOPE-'+uuid.uuid4().hex[:10],title='Isolated approval scope test',levelId=club['LevelId'],maximumHolders=100,requireActiveMembership=True,approvalRequired=True))['id']
 memberclub=next((x for x in clubs if within(x,club['Id'])),club)
 person=sql('SELECT "PersonId" FROM "Memberships" WHERE "ClubId"=\''+memberclub['Id']+'\' AND "Status"=\'Active\' LIMIT 1')
 a=req(admin,'/officer-appointments','POST',dict(orgId=club['Id'],personId=person,positionId=positions[club['LevelId']],startsAt=(now-datetime.timedelta(minutes=1)).isoformat(),endsAt=(now+datetime.timedelta(days=5)).isoformat(),method='Appointed',notes='Isolated QA scope test'))
 r=a['request'];req(admin,'/governance/requests/'+r['id']+'/submit','POST',{'version':r['version']});return a['id'],r['id']
def application(club):
 pid=str(uuid.uuid4());aid=str(uuid.uuid4())
 sql('INSERT INTO "Persons" SELECT (jsonb_populate_record(NULL::"Persons",to_jsonb(p)||jsonb_build_object(\'Id\',\''+pid+'\',\'UserId\',NULL))).* FROM "Persons" p LIMIT 1')
 sql('INSERT INTO "MembershipApplications" SELECT (jsonb_populate_record(NULL::"MembershipApplications",to_jsonb(a)||jsonb_build_object(\'Id\',\''+aid+'\',\'PersonId\',\''+pid+'\',\'ClubId\',\''+club['Id']+'\',\'TenantId\',\''+club['TenantId']+'\',\'CategoryId\',(SELECT "Id" FROM "MembershipCategories" WHERE "TenantId"=\''+club['TenantId']+'\' LIMIT 1),\'WorkflowId\',(SELECT "Id" FROM "MembershipWorkflows" WHERE "TenantId"=\''+club['TenantId']+'\' AND "Kind"=\'Membership\' AND "Active" LIMIT 1),\'Status\',\'Submitted\',\'CurrentSequence\',1,\'Version\',1,\'Revision\',0,\'InvitationId\',NULL))).* FROM "MembershipApplications" a LIMIT 1')
 return aid
for short,role in [('club','Club President'),('zonal','Zonal President'),('district','District President'),('international','International President'),('platform',None)]:
 c,w=(admin,aw) if short=='platform' else login(short+'.president@example.test',role)
 for i,club in enumerate(clubs):
  allowed=short=='platform' or within(club,w['orgId'])
  aid,rid=appointment(club);path='/governance/requests/'+rid
  if allowed:
   d=req(c,path);check(short+' appointment canDecide '+str(i),d['canDecide']);assert d['canDecide']
   req(c,path+'/decisions','POST',dict(decision='Approve',comment='Isolated scope verification',version=d['request']['version']))
   state=req(admin,'/officer-appointments/'+aid)['appointment']['status'];check(short+' appointment activated '+str(i),state=='Active',state)
  else:
   req(c,path,expected=404);req(c,path+'/decisions','POST',dict(decision='Approve',comment='Must deny',version=2),404)
   state=req(admin,'/officer-appointments/'+aid)['appointment']['status'];check(short+' denied appointment unchanged '+str(i),state=='Pending approval',state)
  appid=application(club);path='/membership/applications/'+appid
  if allowed:
   d=req(c,path);check(short+' member canDecide '+str(i),d['canDecide']);assert d['canDecide']
   for stage in range(5):
    d=req(c,path)
    if d['application']['status']=='Approved':break
    req(c,path+'/decisions','POST',dict(decision='Approve',comment='Isolated scope verification',version=d['application']['version']))
   d=req(c,path);check(short+' membership approved '+str(i),d['application']['status']=='Approved',d['application']['status'])
  else:
   req(c,path,expected=404);req(c,path+'/decisions','POST',dict(decision='Approve',comment='Must deny',version=1),404)
 print(short,'scope checks completed',flush=True)
# Test the president's own organizational level and deny its parent.
for short,role in [('club','Club President'),('zonal','Zonal President'),('district','District President'),('international','International President')]:
 c,w=login(short+'.president@example.test',role)
 root=byid[w['orgId']]
 aid,rid=appointment(root)
 d=req(c,'/governance/requests/'+rid);check(short+' own-level canDecide',d['canDecide'])
 req(c,'/governance/requests/'+rid+'/decisions','POST',dict(decision='Approve',comment='Own-level scope test',version=d['request']['version']))
 check(short+' own-level active',req(admin,'/officer-appointments/'+aid)['appointment']['status']=='Active')
 if root['ParentId']:
  aid,rid=appointment(byid[root['ParentId']])
  req(c,'/governance/requests/'+rid,expected=404)
  req(c,'/governance/requests/'+rid+'/decisions','POST',dict(decision='Approve',comment='Ancestor must deny',version=2),404)
print('Own organizational levels and ancestor denials completed',flush=True)
result=dict(total=len(checks),passed=sum(x['passed'] for x in checks),failed=[x for x in checks if not x['passed']],checks=checks)
pathlib.Path('docs/qa/approval-scope').mkdir(parents=True,exist_ok=True);pathlib.Path('docs/qa/approval-scope/results.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='checks'},indent=2));sys.exit(bool(result['failed']))
