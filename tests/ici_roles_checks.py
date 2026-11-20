"""ICI source-role acceptance checks. Uses only the disposable QA database."""
import os,json,pathlib,subprocess,time,uuid,datetime,urllib.request,hashlib,base64,struct
import support
DB='clubs_ici_roles_qa_20261116'
ASSEMBLY=str(pathlib.Path(os.environ.get('CLUBS_TEST_ASSEMBLY','.infra/ici-role-build/Clubs.Api.dll')).resolve())
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
subprocess.run(['dotnet',ASSEMBLY,'--migrate'],env=migrationEnv,check=True,capture_output=True)

sql('''UPDATE "CommunicationAccounts" SET "Enabled"=false;
UPDATE "Mails" SET "DeadLetter"=true WHERE "SentAt" IS NULL;
UPDATE "CommunicationBroadcasts" SET "Status"='Cancelled';
UPDATE "CommunicationAutomations" SET "Status"='Cancelled';''')
sql('''UPDATE "Users" SET "PasswordHash"='''+"'"+identity_hash()+"'"+''',"FailedAttempts"=0,"LockedUntil"=null WHERE "Email" IN ('admin@example.test','club.president@example.test','regional.president@example.test','national.president@example.test')''')
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+DB+';Username=clubs','Authentication__MemberOtpEnabled':'false','Smtp__Host':'','Communications__MockBase':'http://127.0.0.1:5091','Logging__LogLevel__Default':'Warning'}
log=open('.infra/ici-qa.log','w',encoding='utf-8')
process=subprocess.Popen(['dotnet',ASSEMBLY,'--urls',support.BASE],env=env,stdout=log,stderr=log)
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
  check(label+' workspace context shows assigned regions only',all(o['id']==expected or o['parentId']==expected for o in units) and len(units)>=125)
  if client is ns: continue
  check(label+' hierarchy picker preserves coverage',good(client,'/membership/hierarchy?kind=Region')['total']==1)
  foreign=sql('SELECT "Id" FROM "Memberships" WHERE "ClubId"=\''+(c1 if client is ob else c2)+"' LIMIT 1")
  check(label+' cannot fetch another area member',client.req('/members/'+foreign)[0] in (403,404))
 foreign=sql('SELECT "Id" FROM "Memberships" WHERE "ClubId"=\''+c2+"' LIMIT 1")
 for label,client in [('National President',np),('National Secretary',ns),('National Treasurer',nt)]:
  check(label+' can list members outside the first region',good(client,'/members?regionId='+r2)['total']>0)
  detail=good(client,'/members/'+foreign)
  check(label+' can view member details and club hierarchy',detail['membership']['id']==foreign and bool(good(client,'/membership/hierarchy?clubId='+c2)['selectedPath']))
  check(label+' has contact and personal profile read grants',any(w['role']==label and {'members.personal.view','members.contacts.view'} <= set(w['permissions']) and 'members.manage' not in w['permissions'] for w in good(client,'/workspaces')))
 for label,client in [('Regional President',rp),('Club President',cp)]:
  check(label+' can list own club members',good(client,'/members?clubId='+c1)['total']>0)
  check(label+' cannot list or open another region member',good(client,'/members?clubId='+c2)['total']==0 and client.req('/members/'+foreign)[0]==404)
 sibling=sql(f'''SELECT m."Id" FROM "Memberships" m JOIN "Orgs" o ON o."Id"=m."ClubId" WHERE o."ParentId"='{r1}' AND o."Id"<>'{c1}' LIMIT 1''')
 check('Regional President can open a member of another club in the same region',bool(sibling) and rp.req('/members/'+sibling)[0]==200)
 check('Club President cannot open a member of another club in the same region',bool(sibling) and cp.req('/members/'+sibling)[0]==404)
 check('Directors have no member directory or finance management',all(c.req('/members')[0]==403 and not any('finance.manage' in w['permissions'] for w in good(c,'/workspaces') if w['role']!='Club Member') for c in [nd,rd,nc]))
 club_options=good(cp,'/membership/options')
 registered=good(cp,'/members','POST',{'person':{'legalName':'QA Benevolent '+tag,'displayName':'QA Benevolent '+tag},'clubId':c1,'categoryId':club_options['categories'][0]['id'],'benevolentFund':True})
 fund_member='/members/'+registered['id']
 fund_record=good(cp,fund_member)['membership']
 check('Benevolent Fund Yes is saved at registration',fund_record['benevolentFund'] is True)
 fund_update={'benevolentFund':False,'version':fund_record['version']}
 check('Regional President has read-only Benevolent Fund access',good(rp,fund_member)['membership']['benevolentFund'] is True and rp.req(fund_member+'/benevolent-fund','PUT',fund_update)[0]==403)
 good(cp,fund_member+'/benevolent-fund','PUT',fund_update)
 check('Benevolent Fund No is saved and visible nationally',good(nt,fund_member)['membership']['benevolentFund'] is False)
 check('Benevolent Fund rejects stale changes',cp.req(fund_member+'/benevolent-fund','PUT',fund_update)[0]==409)
 check('Existing membership has no recorded choice',good(np,'/members/'+foreign)['membership']['benevolentFund'] is None)
 annual='/finance/annual-dues'
 platform_email,platform_id=fake('dues-platform')
 sql(f'''UPDATE "Users" SET "PlatformAdmin"=true WHERE "Id"='{platform_id}' ''')
 platform=login(platform_email,'Club Member')
 settings=good(platform,annual+'/settings')
 check('Annual dues default to March 31 and two INR fees',settings['expiryMonth']==3 and settings['expiryDay']==31 and settings['firstHalfFee']==500 and settings['secondHalfFee']==250)
 settings_input={key:settings[key] for key in ('expiryMonth','expiryDay','firstHalfFee','secondHalfFee','version')}
 president_write=rp.req(annual+'/settings','PUT',settings_input)
 check('Only Platform Admin can change annual dues settings',settings['canConfigure'] and not good(cp,annual+'/settings')['canConfigure'] and president_write[0]==403,president_write)
 check('Club President cannot record payment outside their club',cp.req(annual+'/members/'+foreign+'/payment','PUT',{'paid':True})[0]==404)
 check('Regional President cannot activate outside their region',rp.req(annual+'/members/'+foreign+'/activate','POST',{'version':1})[0]==404)
 membership_path=annual+'/members/'+registered['id']
 good(cp,membership_path+'/payment','PUT',{'paid':True})
 good(cp,membership_path+'/payment','PUT',{'paid':True})
 paid=good(rp,annual+'/members?regionId='+r1+'&clubId='+c1+'&status=paid&search=QA%20Benevolent%20'+tag)
 check('Regional dues report shows the actual fee and counts the payment once',paid['total']==1 and paid['items'][0]['amount']==settings['currentFee'] and paid['items'][0]['paid'])
 good(cp,membership_path+'/payment','PUT',{'paid':False})
 unpaid=good(rp,annual+'/members?regionId='+r1+'&clubId='+c1+'&status=unpaid&search=QA%20Benevolent%20'+tag)
 check('Unpaid report includes a member after correction',unpaid['total']==1 and not unpaid['items'][0]['paid'] and unpaid['totals']['collected']==paid['totals']['collected']-settings['currentFee'])
 actual_date=unpaid['cycleStart']
 good(cp,membership_path+'/payment','PUT',{'paid':True,'paidOn':actual_date,'cycleYear':unpaid['cycleYear']})
 dated_payment=good(cp,annual+'/members?clubId='+c1+'&status=paid&search=QA%20Benevolent%20'+tag)['items'][0]
 check('Late-entered dues use the actual date and first-half fee',dated_payment['paidOn']==actual_date and dated_payment['amount']==settings['firstHalfFee'])
 csv_status,csv_body,_=cp.request(annual+'/members?clubId='+c1+'&status=paid&format=csv&search=QA%20Benevolent%20'+tag)
 check('Dues CSV includes the selected scope and actual payment date',csv_status==200 and actual_date.encode() in csv_body and registered['number'].encode() in csv_body)
 prior=good(cp,annual+'/members?clubId='+c1+'&cycleYear='+str(unpaid['cycleYear']-1)+'&search=QA%20Benevolent%20'+tag)
 check('Prior-year dues exclude memberships created later',prior['historical'] and prior['total']==0)
 good(cp,membership_path+'/payment','PUT',{'paid':False})
 current=good(cp,fund_member)['membership']
 good(cp,fund_member+'/status','POST',{'status':'Inactive','version':current['version'],'reason':'QA activation check'})
 inactive=good(cp,fund_member)['membership']
 good(rp,membership_path+'/activate','POST',{'version':inactive['version']})
 activated=good(cp,fund_member)['membership']
 check('Regional President activates a club member through the cycle end',activated['status']=='Active' and activated['endsAt'].startswith(settings['expiryDate']))
 changed_settings={**settings_input,'expiryDay':30,'firstHalfFee':550,'secondHalfFee':275}
 good(platform,annual+'/settings','PUT',changed_settings)
 changed=good(platform,annual+'/settings')
 check('Platform Admin can edit date and fees',changed['expiryDay']==30 and changed['firstHalfFee']==550 and changed['secondHalfFee']==275 and good(cp,fund_member)['membership']['endsAt'].startswith(changed['expiryDate']))
 good(platform,annual+'/settings','PUT',{**settings_input,'version':changed['version']})
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
 positions='/ici/delegated-positions'
 new_rp=login(incoming,'Regional President');new_np=login(incomingNp,'National President')
 regional_units=good(new_rp,positions+'/units')
 check('Regional President can open only clubs in their region',bool(regional_units) and all(u['level']=='Club' and u['parentId']==r1 for u in regional_units))
 check('Club President cannot manage delegated positions',cp.req(positions+'/units')[0]==403)
 check('Regional President cannot manage other regions or their regional office',new_rp.req(positions+'/'+c2)[0]==404 and new_rp.req(positions+'/'+r1)[0]==404)
 check('National President can select a region and manage director portfolios',good(new_np,positions+'/'+r1)['roles']==['Regional President','Regional Director'])
 from openpyxl import Workbook
 import io
 excel_email='ici-excel-'+tag+'@example.test'
 workbook=Workbook()
 workbook.active.append(['Name','Email'])
 workbook.active.append(['QA Excel '+tag,excel_email])
 workbook.active.append(['QA Missing Email '+tag,None])
 stream=io.BytesIO();workbook.save(stream)
 code,job=cp.upload('/membership/imports','members.xlsx',stream.getvalue(),{'clubId':c1,'categoryId':club_options['categories'][0]['id'],'requestKey':str(uuid.uuid4())})
 assert code==200,(code,job)
 job_path='/membership/imports/'+job['id']
 assert cp.req(job_path+'/mapping','POST',{'fields':{'name':'Name','email':'Email'}})[0]==202
 def import_state(status):
  for _ in range(100):
   data=good(cp,job_path)
   if data['status']==status:return data
   if data['status'] in ['Failed','NeedsReview']:raise AssertionError(data)
   time.sleep(.2)
  raise AssertionError(data)
 import_state('Ready')
 assert cp.req(job_path+'/commit','POST',{})[0]==202
 check('Excel import completes with a linked but unverified account',import_state('Completed')['importedRows']==2 and sql(f'''SELECT count(*) FROM "Users" u JOIN "Persons" p ON p."UserId"=u."Id" WHERE u."Email"='{excel_email}' AND NOT u."Verified"''')=='1')
 candidates=good(new_rp,positions+'/'+c1+'/members?search=QA%20Excel%20'+tag)
 check('Imported active member appears in the appointment picker before first sign-in',candidates['total']==1 and candidates['items'][0]['accountLinked'])
 account_report=good(cp,job_path+'/accounts?status=issues')
 check('Completed imports identify email issues and show linked totals',account_report['total']==1 and account_report['items'][0]['state']=='MissingEmail' and any(r['state']=='Linked' and r['count']==1 for r in account_report['summary']))
 repair_id=account_report['items'][0]['membershipId']
 repair_name='QA Missing Email '+tag
 good(cp,'/members/'+repair_id+'/person','PUT',{'legalName':repair_name,'displayName':repair_name,'contacts':[{'kind':'Email','label':'Primary email','value':'ici-repaired-'+tag+'@example.test'}]})
 good(cp,job_path+'/accounts/recheck','POST',{})
 repaired=good(cp,job_path+'/accounts?status=issues')
 check('Corrected import email becomes eligible without a second import',repaired['total']==0 and any(r['state']=='Linked' and r['count']==2 for r in repaired['summary']))
 page_one=good(new_rp,positions+'/'+c1+'/members')
 page_two=good(new_rp,positions+'/'+c1+'/members?page=2')
 check('Appointment picker returns counts and non-overlapping pages',page_one['total']==page_two['total'] and page_two['page']==2 and not set(m['id'] for m in page_one['items']) & set(m['id'] for m in page_two['items']))
 imported_id=candidates['items'][0]['id']
 manual_email='ici-manual-'+tag+'@example.test'
 manual=good(cp,'/members','POST',{'person':{'legalName':'QA Manual '+tag,'displayName':'QA Manual '+tag,'contacts':[{'kind':'Email','label':'Primary email','value':manual_email},{'kind':'Email','label':'Alternate email','value':'ici-alternate-'+tag+'@example.test'}]},'clubId':c1,'categoryId':club_options['categories'][0]['id']})
 check('Manual registration provisions the primary email immediately',manual['loginAvailable'] and sql(f'''SELECT u."Email" FROM "Users" u JOIN "Persons" p ON p."UserId"=u."Id" WHERE p."Id"='{manual["personId"]}' ''')==manual_email)
 member_client=support.Client()
 assert member_client.req('/auth/forgot-password','POST',{'email':excel_email})[0]==202
 token=sql(f'''SELECT split_part(split_part(m."Link",'token=',2),'&',1) FROM "Mails" m JOIN "Users" u ON u."Id"=m."UserId" WHERE u."Email"='{excel_email}' AND m."Kind"='reset' ORDER BY m."CreatedAt" DESC LIMIT 1''')
 good(member_client,'/auth/complete','POST',{'token':token,'password':support.PASSWORD})
 member_client=login(excel_email,'Club Member')
 club_peers=good(member_client,'/membership/me/club-members?clubId='+c1+'&search=QA%20Manual%20'+tag)
 check('Club member can see active peers in own club only',club_peers['total']==1 and list(club_peers['items'][0])==['id','name'] and member_client.req('/membership/me/club-members?clubId='+c2)[0]==404)
 card_available=good(member_client,'/my/id-card/availability?membershipId='+imported_id)
 check('ID download availability matches active ownership',card_available['canDownload'] and member_client.req('/my/id-card/availability?membershipId='+manual['id'])[0]==404)
 pdf_status,pdf,_=member_client.request('/my/id-card/download-latest?membershipId='+imported_id)
 check('Imported active member downloads their own PDF card',pdf_status==200 and pdf.startswith(b'%PDF') and member_client.request('/my/id-card/download-latest?membershipId='+manual['id'])[0]==409)
 officer_email,officer_uid=fake('delegated-officer')
 sql(f'''UPDATE "Persons" SET "UserId"='{officer_uid}' WHERE "Id"='{registered["personId"]}' ''')
 office=positions+'/'+c1
 members=good(new_rp,office+'/members?search=QA%20Benevolent%20'+tag)
 check('Appointment search lists active members with linked accounts',any(m['id']==registered['id'] for m in members['items']))
 original=next((h for h in good(new_rp,office)['holders'] if h['role']=='Club Secretary'),None)
 appointment={'role':'Club Secretary','portfolio':'','membershipId':registered['id'],'currentAssignmentId':original['id'] if original else None}
 check('Presidents cannot appoint a member from another club',new_np.req(office,'PUT',{**appointment,'membershipId':foreign})[0]==404)
 good(new_rp,office,'PUT',appointment)
 secretary=login(officer_email,'Club Secretary')
 check('Club appointee can activate the assigned workspace',any(w['role']=='Club Secretary' and w['orgId']==c1 for w in good(secretary,'/workspaces')))
 check('The same club position cannot be overwritten from a stale screen',new_np.req(office,'PUT',appointment)[0]==409)
 assigned=next(h for h in good(new_np,office)['holders'] if h['role']=='Club Secretary')
 good(new_np,office,'PUT',{**appointment,'currentAssignmentId':assigned['id'],'membershipId':None})
 check('Vacating a position revokes that workspace',all(w['role']!='Club Secretary' for w in good(secretary,'/workspaces')))
 region_office=positions+'/'+r1
 good(new_np,region_office,'PUT',{'role':'Regional Director','portfolio':'Training','membershipId':registered['id'],'currentAssignmentId':None})
 check('National President can appoint a Regional Director in a specific portfolio',good(login(officer_email,'Regional Director'),'/ici/context')['portfolio']=='Training')
 pathlib.Path('.infra/ici-qa-actors.json').write_text(json.dumps({'emails':emails,'assignments':assignments,'tag':tag},indent=2))
 print('Completed '+str(len(results))+' role acceptance checks.',flush=True)
finally:
 if os.environ.get('ICI_QA_KEEP_RUNNING')!='1':process.terminate();process.wait(timeout=20);log.close()

