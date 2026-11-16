"""ICI source-role acceptance checks. Uses only the disposable QA database."""
import os,json,pathlib,subprocess,time,uuid,datetime,urllib.request,hashlib,base64,struct
import support
DB='clubs_ici_roles_qa_20261116'
support.BASE='http://127.0.0.1:5090'
support.PASSWORD='Ici-Test-Password-2026!'
from support import Client
sql=lambda q:support.sql(q,DB)
results=[]
tag=uuid.uuid4().hex[:8]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)})
 pathlib.Path('test-results/ici-roles.json').write_text(json.dumps(results,indent=2))
 print(('PASS ' if ok else 'FAIL ')+name,flush=True)
 assert ok,(name,data)
def good(c,path,method='GET',body=None):
 code,data=c.req(path,method,body);assert code in (200,204),(path,code,data);return data
def login(email,role=None):
 c=Client();good(c,'/auth/login','POST',{'email':email,'password':support.PASSWORD})
 if role:good(c,'/workspaces/'+next(w['id'] for w in good(c,'/workspaces') if w['role']==role)+'/activate','POST',{})
 return c
def identity_hash():
 salt=os.urandom(16)
 return base64.b64encode(b'\x01'+struct.pack('>III',2,100000,16)+salt+hashlib.pbkdf2_hmac('sha512',support.PASSWORD.encode(),salt,100000,32)).decode()
assert DB.startswith('clubs_ici_roles_qa_')
# Recreate this one disposable database for repeatable acceptance runs.
support.sql("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='"+DB+"' AND pid<>pg_backend_pid()",'postgres')
for command in [[str(pathlib.Path('.infra/pgsql/bin/dropdb.exe').resolve()),'-h','127.0.0.1','-p','55432','-U','clubs','--if-exists',DB],[str(pathlib.Path('.infra/pgsql/bin/createdb.exe').resolve()),'-h','127.0.0.1','-p','55432','-U','clubs','-T','clubs_four_level_qa_20261116',DB]]:subprocess.run(command,check=True,capture_output=True)
migrationEnv={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+DB+';Username=clubs'}
subprocess.run(['dotnet',str(pathlib.Path('.infra/ici-role-build/Clubs.Api.dll').resolve()),'--migrate'],env=migrationEnv,check=True,capture_output=True)

sql('''UPDATE "CommunicationAccounts" SET "Enabled"=false;
UPDATE "Mails" SET "DeadLetter"=true WHERE "SentAt" IS NULL;
UPDATE "CommunicationBroadcasts" SET "Status"='Cancelled';
UPDATE "CommunicationAutomations" SET "Status"='Cancelled';''')
sql('''UPDATE "Users" SET "PasswordHash"='''+"'"+identity_hash()+"'"+''',"FailedAttempts"=0,"LockedUntil"=null WHERE "Email" IN ('admin@example.test','club.president@example.test','regional.president@example.test','national.president@example.test')''')
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+DB+';Username=clubs','Authentication__MemberOtpEnabled':'false','Smtp__Host':'','Communications__MockBase':'http://127.0.0.1:5091','Logging__LogLevel__Default':'Warning'}
log=open('.infra/ici-qa.log','w',encoding='utf-8')
process=subprocess.Popen(['dotnet',str(pathlib.Path('.infra/ici-role-build/Clubs.Api.dll').resolve()),'--urls',support.BASE],env=env,stdout=log,stderr=log)
try:
 for _ in range(100):
  try:urllib.request.urlopen(support.BASE+'/health');break
  except:time.sleep(.2)
 admin=login('admin@example.test','Association Super Admin')
 cp=login('club.president@example.test','Club President');rp=login('regional.president@example.test','Regional President');np=login('national.president@example.test','National President')
 ctx=good(admin,'/ici/context');roles={r['name']:r['id'] for r in good(admin,'/roles')}
 check('All 11 source roles and their feature definitions exist',len(ctx['definitions'])==11 and all(d['name'] in roles for d in ctx['definitions']))
 check('Four-level hierarchy retained',set(l['name'] for l in good(admin,'/levels'))=={'International','National','Region','Club'})
 org=lambda code:sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\''+code+"'")
 root=org('IMPACT-HQ');nat=org('DEMO-NAT-01');r1=org('DEMO-R01');r2=org('DEMO-R02');r3=org('DEMO-R03');c1=org('DEMO-R01-D01-Z01-C01');c2=org('DEMO-R02-D01-Z01-C01')
 tenant=sql('SELECT "TenantId" FROM "Orgs" WHERE "Id"=\''+root+"'")
 def fake(label):
  uid=str(uuid.uuid4());email='ici-'+label+'-'+tag+'@example.test';aid=str(uuid.uuid4())
  sql(f'''INSERT INTO "Users" ("Id","Email","PasswordHash","Verified","PlatformAdmin","FailedAttempts") VALUES ('{uid}','{email}','{identity_hash()}',true,false,0);
INSERT INTO "UserProfiles" ("Id","Name","Phone","Locale","TimeZone","Theme") VALUES ('{uid}','QA ICI {label}','','en-IN','Asia/Kolkata','system');
INSERT INTO "Assignments" ("Id","TenantId","UserId","OrgId","RoleId","IciPortfolio","IciSlot") VALUES ('{aid}','{tenant}','{uid}','{c1}','{roles['Club Member']}','','');''')
  return email,uid
 actors={};assignments={};emails={}
 def appoint(label,role,orgId,portfolio='',slot='',regions=None):
  email,uid=fake(label);row=good(admin,'/ici/team','POST',{'email':email,'roleId':roles[role],'orgId':orgId,'portfolio':portfolio,'slot':slot,'regionIds':regions or []})
  actors[label]=login(email,role);assignments[label]=row;emails[label]=email;return actors[label]
 eb=appoint('board','Executive Board',root);eb2=appoint('board2','Executive Board',root)
 nt=appoint('treasurer','National Treasurer',nat)
 nd=appoint('training','National Director',nat,'Training');ndb=appoint('business','National Director',nat,'Business')
 rd=appoint('rd','Regional Director',r1,'Business');nc=appoint('eps','National Coordinator',nat,'EPS')
 nvp=appoint('nvp1','National Vice President',nat,slot='NVP 1',regions=[r1]);nvp2=appoint('nvp2','National Vice President',nat,slot='NVP 2',regions=[r2])
 ob=appoint('office','Office Bearers',nat,regions=[r2]);ns=appoint('secretary','National Secretary',nat,regions=[r1])
 rp2=appoint('rp2','Regional President',r2)
 for label,client in {'club':cp,'region':rp,'national':np,**actors}.items():
  context=good(client,'/ici/context');check(label+' workspace loads with communications denied',len(context['definitions'])==11 and client.req('/communications/member-scope')[0]==403)
 for label,client in [('NVP',nvp),('Office Bearer',ob),('Secretary',ns)]:
  expected=r2 if client is ob else r1
  units=good(client,'/ici/context')['units']
  check(label+' sees assigned regions only',all(o['id']==expected or o['parentId']==expected for o in units) and len(units)>=125)
  check(label+' hierarchy picker preserves coverage',good(client,'/membership/hierarchy?kind=Region')['total']==1)
  foreign=sql('SELECT "Id" FROM "Memberships" WHERE "ClubId"=\''+(c1 if client is ob else c2)+"' LIMIT 1")
  check(label+' cannot fetch another area member',client.req('/members/'+foreign)[0] in (403,404))
 check('Directors and Treasurer have no member directory or finance management',all(c.req('/members')[0]==403 and not any('finance.manage' in w['permissions'] for w in good(c,'/workspaces') if w['role']!='Club Member') for c in [nd,rd,nc,nt]))
 badEmail,_=fake('invalid')
 check('Director assignment requires a valid portfolio',admin.req('/ici/team','POST',{'email':badEmail,'roleId':roles['National Director'],'orgId':nat})[0]==400)
 check('Generic assignment endpoint cannot bypass portfolio validation',admin.req('/assignments','POST',{'email':badEmail,'roleId':roles['National Director'],'orgId':nat})[0]==400)
 check('Source role definitions cannot be renamed or customized',admin.req('/roles/'+roles['National Coordinator'],'PUT',{'name':'Custom Coordinator','permissions':[]})[0]==409)
 sql('DELETE FROM "IciRegionCoverages" WHERE "AssignmentId"=\''+assignments['office']['id']+"'")
 check('Missing assigned-region coverage never falls back to national access',good(ob,'/members')['total']==0 and good(ob,'/ici/reports')['total']==0)
 good(admin,'/ici/team/'+assignments['office']['id']+'/scope','PUT',{'portfolio':'','slot':'','regionIds':[r2]})
 check('NVP cannot reuse an occupied region',admin.req('/ici/team','POST',{'email':badEmail,'roleId':roles['National Vice President'],'orgId':nat,'slot':'NVP 3','regionIds':[r1]})[0]==409)
 check('Source leaders cannot directly grant roles',np.req('/ici/team','POST',{'email':badEmail,'roleId':roles['Executive Board'],'orgId':root})[0]==403)
 now=datetime.datetime.now(datetime.timezone.utc)
 def event(client,orgId,kind='Training',program='',level='Club',future=False):
  start=now+datetime.timedelta(days=2 if future else -2)
  return good(client,'/events','POST',{'orgId':orgId,'typeId':'00000000-0000-0000-0000-000000000000','title':'QA ICI '+kind+' '+tag,'startsAt':start.isoformat(),'endsAt':(start+datetime.timedelta(hours=2)).isoformat(),'registrationStartsAt':(start-datetime.timedelta(days=3)).isoformat(),'registrationEndsAt':start.isoformat(),'timeZone':'Asia/Kolkata','venue':'QA Hall','activityType':kind,'activityProgram':program,'activityLevel':level,'activityClubId':c1 if orgId==r1 else orgId,'description':'Isolated ICI role checks','projectCoordinator':'QA Coordinator','coordinatorPhone':'+919000000001','coordinatorEmail':'qa@example.test','memberParticipants':20,'outsideVolunteers':4,'meetingCost':100})
 training=event(cp,c1);business=event(cp,c1,'Business');eps=event(cp,c1,'Service','EPS');foreignEvent=event(admin,c2,'Business');regional=event(rp,r1,'Training',level='Region');future=event(cp,c1,future=True)
 for label,client,include,exclude in [('ND',nd,training,business),('RD',rd,business,foreignEvent),('Coordinator',nc,eps,training)]:
  rows=good(client,'/ici/reports')['items'];ids={r['id'] for r in rows}
  check(label+' activity reports enforce portfolio and region',include['id'] in ids and exclude['id'] not in ids)
  check(label+' direct event URL cannot bypass scope',client.req('/events/'+exclude['id'])[0] in (403,404))
  check(label+' ordinary event list also enforces portfolio scope',exclude['id'] not in {e['id'] for e in good(client,'/events?manage=true')['items']})
 check('Treasurer initially sees no unrelated events',training['id'] not in {r['id'] for r in good(nt,'/ici/reports')['items']})
 def request(client,kind,orgId=c1,**kw):return good(client,'/ici/requests','POST',{'kind':kind,'orgId':orgId,'title':'QA ICI '+kind+' '+tag,'description':'Isolated request purpose and details',**kw})
 def action(client,row,act,comment='QA independently reviewed'):
  return good(client,'/ici/requests/'+row['id']+'/actions','POST',{'action':act,'version':row['version'],'comment':comment})
 def current(client,row):return good(client,'/ici/requests/'+row['id'])['request']
 aid=request(cp,'FinancialAid',eventId=training['id'],amount=5000);aid=action(cp,aid,'Submit')
 check('Treasurer sees submitted financial aid and its event only',aid['id'] in {r['id'] for r in good(nt,'/ici/requests')['items']} and training['id'] in {r['id'] for r in good(nt,'/ici/reports')['items']} and business['id'] not in {r['id'] for r in good(nt,'/ici/reports')['items']})
 check('Treasurer cannot approve financial aid',nt.req('/ici/requests/'+aid['id']+'/actions','POST',{'action':'Approve','version':aid['version']})[0]==403)
 check('Financial aid routes to Executive Board',action(eb,aid,'Approve')['status']=='Approved')
 title=request(cp,'TitleClearance',targetOrgId=r1,amount=1,reference='TEST RECEIPT ONLY')
 check('Title fee is calculated on the server',title['amount']==100)
 title=action(cp,title,'Submit')
 check('Unverified title payment blocks approval',np.req('/ici/requests/'+title['id']+'/actions','POST',{'action':'Approve','version':title['version']})[0]==409)
 title=action(admin,title,'VerifyPayment');title=action(np,title,'Approve')
 check('Paid title clearance requires independent approval',title['status']=='Approved')
 creation=request(np,'ClubCreation',r1,title=title['title'],targetOrgId=r1,relatedRequestId=title['id'],code='QA-ICI-'+tag)
 creation=action(np,creation,'Submit');creation=action(eb,creation,'Approve')
 check('Club creation applies approved clearance in four-level hierarchy',bool(creation['resultId']) and sql('SELECT "ParentId" FROM "Orgs" WHERE "Id"=\''+creation['resultId']+"'")==r1)
 mapping=request(np,'ClubMapping',creation['resultId'],targetOrgId=r2);mapping=action(np,mapping,'Submit');action(eb,mapping,'Approve')
 check('Approved club mapping changes actual regional parent',sql('SELECT "ParentId" FROM "Orgs" WHERE "Id"=\''+creation['resultId']+"'")==r2)
 rm=request(np,'RegionMapping',r3,targetOrgId=assignments['nvp1']['id']);rm=action(np,rm,'Submit');action(eb,rm,'Approve')
 check('Region-to-NVP mapping updates live authorization scope',r3 in {o['id'] for o in good(nvp,'/ici/context')['units']})
 report=request(cp,'EventReport',eventId=training['id'],memberCount=20,volunteerCount=4,hours=2);report=action(cp,report,'Submit')
 check('Club report routes to Regional President',action(rp,report,'Approve')['status']=='Approved')
 check('Future activities cannot be reported as completed',cp.req('/ici/requests','POST',{'kind':'EventReport','orgId':c1,'title':'Invalid future report','eventId':future['id']})[0]==400)
 for client,orgId,reviewer,ev in [(cp,c1,np,training),(rp,r1,eb,regional)]:
  bid=request(client,'EventBid',orgId,eventId=ev['id']);bid=action(client,bid,'Submit');check('Event bid follows '+('club' if client is cp else 'region')+' approval authority',action(reviewer,bid,'Approve')['status']=='Approved')
 # Publication uses the existing governance engine, with distinct actors and ordered stages.
 def publish_proposal(client,ev):return good(client,'/events/'+ev['id']+'/status','POST',{'action':'Submit','version':ev['version']})
 def governance(client,proposal,decision='Approve'):
  req=good(admin,'/governance/requests/'+proposal['approvalRequestId'])['request']
  return client.req('/governance/requests/'+req['id']+'/decisions','POST',{'decision':decision,'comment':'QA role workflow verification','version':req['version']})
 clubProposal=publish_proposal(cp,training)
 check('National Director cannot bypass Regional President stage',governance(nd,clubProposal)[0]==403)
 check('Regional President completes first club-event stage',governance(rp,clubProposal)[0]==200)
 check('Wrong portfolio cannot approve club event',governance(ndb,clubProposal)[0] in (403,404))
 check('Same actor cannot approve both event stages',governance(rp,clubProposal)[0]==403)
 check('Matching National Director completes club event',governance(nd,clubProposal)[0]==200)
 regionProposal=publish_proposal(rp,regional)
 check('Executive Board cannot bypass National Director stage',governance(eb,regionProposal)[0]==403)
 check('Regional event first approved by matching National Director',governance(nd,regionProposal)[0]==200)
 check('Executive Board finalizes regional event',governance(eb,regionProposal)[0]==200)
 secret='FICTIONAL-CASE-SECRET-'+tag
 legal=request(eb,'LegalAction',root,description=secret);legal=action(eb,legal,'Submit')
 check('Legal records are inaccessible to National President',np.req('/ici/requests/'+legal['id'])[0]==404)
 check('Legal narrative is encrypted at rest',secret not in sql('SELECT "Payload"||"Description" FROM "IciRequests" WHERE "Id"=\''+legal['id']+"'"))
 check('Board cannot approve their own legal case',eb.req('/ici/requests/'+legal['id']+'/actions','POST',{'action':'Approve','version':legal['version']})[0]==403)
 check('Independent Board review can approve legal record',action(eb2,legal,'Approve')['status']=='Approved')
 bank='1234567890123456'
 profile=good(cp,'/ici/profile');good(cp,'/ici/profile','PUT',{'trainingCareer':'QA Train the Trainer','leadershipCareer':'Club President','bankName':'Fictional QA Bank','accountHolder':'QA Test','accountNumber':bank,'routingCode':'QA000001','replaceBankDetails':True,'version':profile['version']})
 own=good(cp,'/ici/profile');check('Own bank response is masked and storage protected',own['accountLast4']=='3456' and bank not in json.dumps(own) and bank not in sql('SELECT "ProtectedBankDetails" FROM "IciPrivateProfiles" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'club.president@example.test\')'))
 check('Bank profile cannot be read using another user ID',good(nd,'/ici/profile?userId='+sql('SELECT "Id" FROM "Users" WHERE "Email"=\'club.president@example.test\''))['accountLast4']=='')
 check('Directors cannot enter bank details',nd.req('/ici/profile','PUT',{'replaceBankDetails':True,'accountNumber':bank})[0]==403)
 # Initial subscription must use real, active member IDs and server-calculated dues.
 cat=sql('SELECT "CategoryId" FROM "Memberships" WHERE "ClubId"=\''+c1+"' LIMIT 1")
 mids=[]
 for i in range(20):
  m=good(admin,'/members','POST',{'clubId':c1,'categoryId':cat,'person':{'legalName':'QA ICI Subscriber '+tag+' '+str(i),'displayName':'QA Subscriber'},'requestKey':str(uuid.uuid4())});mids.append(m['id'])
 check('Initial subscription rejects fewer than 20 members',cp.req('/ici/requests','POST',{'kind':'Subscription','orgId':c1,'title':'Too few initial members','memberIds':mids[:19]})[0]==400)
 sub=request(cp,'Subscription',memberIds=mids,amount=1,reference='QA-MANUAL-RECEIPT');check('Subscription uses INR365 per selected member',sub['amount']==7300 and sub['memberCount']==20)
 sub=action(cp,sub,'Submit');dup=request(cp,'Subscription',memberIds=mids,reference='QA-DUPLICATE')
 check('Same member cannot be charged twice for the same financial year',cp.req('/ici/requests/'+dup['id']+'/actions','POST',{'action':'Submit','version':dup['version']})[0]==409)
 check('Subscription cannot be approved before payment verification',admin.req('/ici/requests/'+sub['id']+'/actions','POST',{'action':'Approve','version':sub['version']})[0]==409)
 sub=action(admin,sub,'VerifyPayment');sub=action(admin,sub,'Approve');check('Verified subscription stores approval and member coverage',sub['status']=='Approved' and sql('SELECT count(*) FROM "IciSubscriptionMembers" WHERE "RequestId"=\''+sub['id']+"'")=='20')
 # Transfers require both regional reviews, both assigned NVPs, then NP/Board.
 membership=good(cp,'/members/'+mids[0])['membership']
 transfer=good(cp,'/members/'+mids[0]+'/transfers','POST',{'destinationClubId':c2,'categoryId':cat,'reason':'QA relocation across regions','version':membership['version']})
 def transfer_action(client,t):return client.req('/membership/transfers/'+t['id']+'/decisions','POST',{'decision':'Approve','comment':'QA transfer stage','version':t['version']})
 check('Transfer begins at source Regional President',transfer['status']=='PendingSource')
 check('National President cannot skip initial transfer reviews',transfer_action(np,transfer)[0]==403)
 check('Other Regional President cannot approve source review',transfer_action(rp2,transfer)[0]==403)
 for client,status in [(rp,'PendingDestination'),(rp2,'PendingVicePresidentSource'),(nvp,'PendingVicePresidentDestination'),(nvp2,'PendingAssociation')]:
  code,transfer=transfer_action(client,transfer);check('Transfer advances to '+status,code==200 and transfer['status']==status,transfer)
  if status=='PendingVicePresidentSource':
   check('Wrong NVP cannot review source region',transfer_action(nvp2,transfer)[0]==403)
   # An in-flight NVP review must retain the unique pending-transfer reservation.
   latest=good(cp,'/members/'+mids[0])['membership']
   check('Second transfer cannot be opened while NVP review is pending',cp.req('/members/'+mids[0]+'/transfers','POST',{'destinationClubId':c2,'categoryId':cat,'reason':'QA duplicate transfer','version':latest['version']})[0]==409)
 code,transfer=transfer_action(np,transfer)
 check('Final National President approval completes transfer',code==200 and transfer['status'] in ['Completed','Scheduled'],transfer)
 # Leadership access is reassigned, without exposing or changing either password.
 incoming,incomingUid=fake('incoming-rp');oldRp=next(t for t in good(admin,'/ici/team')['assignments'] if t['role']=='Regional President' and t['orgId']==r1)
 handover=request(np,'TeamHandover',r1,email=incoming,role='Regional President',oldAssignmentId=oldRp['id']);handover=action(np,handover,'Submit')
 beforeHash=sql('SELECT "PasswordHash" FROM "Users" WHERE "Id"=\''+incomingUid+"'")
 check('National President cannot approve own handover request',np.req('/ici/requests/'+handover['id']+'/actions','POST',{'action':'Approve','version':handover['version']})[0]==403)
 handover=action(eb,handover,'Approve')
 check('Approved leadership handover revokes outgoing assignment',sql('SELECT count(*) FROM "Assignments" WHERE "Id"=\''+oldRp['id']+"'")=='0' and handover['resultId'])
 check('Handover grants incoming official access without password changes',good(login(incoming,'Regional President'),'/ici/context')['role']=='Regional President' and beforeHash==sql('SELECT "PasswordHash" FROM "Users" WHERE "Id"=\''+incomingUid+"'"))
 incomingNp,npUid=fake('incoming-np');oldNp=next(t for t in good(admin,'/ici/team')['assignments'] if t['role']=='National President' and t['orgId']==nat)
 check('National President cannot initiate replacement of own office',np.req('/ici/requests','POST',{'kind':'TeamHandover','orgId':nat,'title':'QA NP replacement','email':incomingNp,'role':'National President','oldAssignmentId':oldNp['id']})[0]==403)
 replacement=request(eb,'TeamHandover',nat,email=incomingNp,role='National President',oldAssignmentId=oldNp['id']);replacement=action(eb,replacement,'Submit');replacement=action(eb2,replacement,'Approve')
 check('Executive Board can independently hand over National President office',replacement['status']=='Approved' and good(login(incomingNp,'National President'),'/ici/context')['role']=='National President')
 pathlib.Path('.infra/ici-qa-actors.json').write_text(json.dumps({'emails':emails,'assignments':assignments,'tag':tag},indent=2))
 print('Completed '+str(len(results))+' role acceptance checks.',flush=True)
finally:
 if os.environ.get('ICI_QA_KEEP_RUNNING')!='1':process.terminate();process.wait(timeout=20);log.close()

