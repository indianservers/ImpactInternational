"""Four-level migration, certifications, president scope and activity regression.
Run only on the disposable clubs_four_level_qa_20261116 database.
"""
import os,json,pathlib,subprocess,time,uuid,datetime,urllib.request
import support
DB='clubs_four_level_qa_20261116'
support.BASE='http://127.0.0.1:5088'
from support import Client
sql=lambda q:support.sql(q,DB)
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)})
 pathlib.Path('test-results/four-level-features.json').write_text(json.dumps(results,indent=2))
 print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,path,method='GET',body=None):
 code,data=c.req(path,method,body);assert code==200,(path,code,data);return data
def login(email):
 c=Client();code,data=c.login(email);assert code==200,(email,code,data);return c
assert DB.startswith('clubs_four_level_qa_')
sql('''UPDATE "CommunicationAccounts" SET "Enabled"=false;
UPDATE "Mails" SET "DeadLetter"=true WHERE "SentAt" IS NULL;
UPDATE "CommunicationBroadcasts" SET "Status"='Cancelled';
UPDATE "CommunicationAutomations" SET "Status"='Cancelled';
UPDATE "Orgs" SET "FiscalStartMonth"=4 WHERE "ParentId" IS NULL;''')
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+DB+';Username=clubs','Authentication__MemberOtpEnabled':'false','Smtp__Host':'','Communications__MockBase':'http://127.0.0.1:5089','Logging__LogLevel__Default':'Warning'}
assembly=str(pathlib.Path('.infra/four-level-build/Clubs.Api.dll').resolve())
log=open('.infra/four-level-qa.log','w',encoding='utf-8');process=subprocess.Popen(['dotnet',assembly,'--urls',support.BASE],env=env,stdout=log,stderr=log)
try:
 for _ in range(100):
  try:urllib.request.urlopen(support.BASE+'/health');break
  except:time.sleep(.2)
 admin=login('admin@example.test');root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin');admin.activate(root)
 levels=good(admin,'/levels');check('Exactly four selectable levels',set(l['name'] for l in levels)=={'International','National','Region','Club'},levels)
 check('All 625 clubs still exist',int(sql('''SELECT count(*) FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."Name"='Club' '''))==625)
 check('All 3125 original memberships retained',int(sql("""SELECT count(*) FROM "Memberships" m JOIN "Persons" p ON p."Id"=m."PersonId" WHERE p."LegalName" NOT LIKE 'QA %' """))==3125)
 check('Every club now reports directly to a region',sql('''SELECT count(*) FROM "Orgs" c JOIN "Levels" l ON l."Id"=c."LevelId" JOIN "Orgs" p ON p."Id"=c."ParentId" JOIN "Levels" pl ON pl."Id"=p."LevelId" WHERE l."IsClub" AND pl."Name"<>'Region' ''')=='0')
 check('Legacy organizational records are archived with snapshots',int(sql('SELECT count(*) FROM "HierarchyRetirementArchive"'))==150 and sql('''SELECT count(*) FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."Name" IN ('Zone','District') AND o."Status"<>'Archived' ''')=='0')
 check('Hierarchy database guard remains enabled',sql('''SELECT count(*) FROM pg_trigger WHERE tgname IN ('organization_hierarchy_guard','level_hierarchy_guard') AND tgenabled='O' ''')=='2')
 for kind in ['Zone','District']:
  check(kind+' cannot be selected through hierarchy API',admin.req('/membership/hierarchy?kind='+kind)[0]==400)
 check('Legacy roles are absent from role catalog',not any(r['name'] in ['Zonal President','District President','Zone Administrator','District Administrator'] for r in good(admin,'/roles')))
 old=login('zonal.president@example.test');check('Removed leadership scope grants no member access',old.req('/members')[0]==403)
 club=login('club.president@example.test');region=login('regional.president@example.test')
 cs=good(club,'/leadership/my-statistics');rs=good(region,'/leadership/my-statistics')
 check('Club President sees exactly their members',cs['total']==int(sql("SELECT count(*) FROM \"Memberships\" WHERE \"ClubId\"='"+cs['organization']['id']+"'")) and cs['clubs']==1 and cs['total']>=5)
 check('Region President sees only their region',rs['total']>=625 and rs['total']<650 and rs['clubs']==125 and all(m['club'].startswith('Demo Region 01') for m in rs['members']),rs['total'])
 outside=sql('''SELECT "Id" FROM "Memberships" WHERE "ClubId"=(SELECT "Id" FROM "Orgs" WHERE "Code"='DEMO-R02-D01-Z01-C01') LIMIT 1''')
 check('Presidents cannot open a member in another region',club.req('/members/'+outside)[0] in (403,404) and region.req('/members/'+outside)[0] in (403,404))
 public=good(admin,'/public/membership-options');check('Public signup offers National, Region, Club only','nationals' in public and 'regions' in public and 'zones' not in public and 'districts' not in public and public['availableClubs']==625)
 chosen=public['clubs'][0]
 signup=Client();signup_code,signup_result=signup.req('/public/membership-signup','POST',{'name':'QA Public Signup','email':'four-level-'+uuid.uuid4().hex[:10]+'@example.test','phone':'+919000000009','password':'Isolated-QA-Password-2026!','confirmation':'Isolated-QA-Password-2026!','agreed':True,'nationalId':chosen['nationalId'],'regionId':chosen['regionId'],'clubId':chosen['id']})
 check('Public signup accepts the four-level path without legacy IDs',signup_code in (200,202) and 'zone' not in str(signup_result).lower(),signup_result)
 programs=good(admin,'/certifications');check('Training certification starter programs available',{'AI','Digital Marketing','Train the Trainer'}<=set(p['name'] for p in programs))
 member=login('member.club1@example.test');other=login('member.club2@example.test')
 check('Members cannot create certification programs',member.req('/certifications','POST',{'name':'Forbidden'})[0]==403)
 check('Presidents cannot create certification programs',region.req('/certifications','POST',{'name':'Forbidden'})[0]==403)
 program=good(admin,'/certifications','POST',{'name':'QA Special Program '+uuid.uuid4().hex[:8],'description':'Isolated test program'})
 own=good(member,'/membership/me')['profile'];other_person=good(other,'/membership/me')['profile']['id']
 details={'programId':program['id'],'certificateId':'qa-'+uuid.uuid4().hex[:10],'batch':'Batch 2026-A','issuer':'ICI Training','trainingCenter':'Regional Training Center','issuedOn':'2026-05-01','validUntil':'2027-05-01','verificationUrl':'https://example.test/certificate','notes':'Practical assessment completed'}
 certificate=good(member,'/member-certifications','POST',details)
 stored=good(member,'/member-certifications');check('Member can save complete certification details',any(r['certification']['id']==certificate['id'] and r['certification']['batch']==details['batch'] for r in stored))
 check('Another member cannot read certificate details',other.req('/member-certifications?personId='+own['id'])[0]==403)
 check('Member cannot edit another person certificate',member.req('/member-certifications?personId='+other_person,'POST',details)[0]==403)
 updated=good(member,'/member-certifications/'+certificate['id'],'PUT',{**details,'batch':'Corrected batch','version':certificate['version']})
 check('Certificate edits use version checks',updated['batch']=='Corrected batch' and member.req('/member-certifications/'+certificate['id'],'PUT',{**details,'version':certificate['version']})[0]==409)
 check('Duplicate certificate numbers rejected',other.req('/member-certifications','POST',details)[0]==409)
 check('Invalid validity dates rejected',member.req('/member-certifications','POST',{**details,'certificateId':'invalid-date','validUntil':'2020-01-01'})[0]==400)
 check('Unsafe verification links rejected',member.req('/member-certifications','POST',{**details,'certificateId':'invalid-link','verificationUrl':'javascript:alert(1)'})[0]==400)
 expired=good(member,'/member-certifications','POST',{**details,'certificateId':'expired-'+uuid.uuid4().hex[:8],'validUntil':'2026-05-02'})
 check('Expired certification is clearly identified',next(r for r in good(member,'/member-certifications') if r['certification']['id']==expired['id'])['status']=='Expired')
 club_id=cs['members'][0]['id'];membership=good(club,'/members/'+club_id)['membership'];category=membership['categoryId'];club_org=membership['clubId']
 payload={'clubId':club_org,'categoryId':category,'person':{'legalName':'QA Certification '+uuid.uuid4().hex[:8],'displayName':'QA Certified Member'},'requestKey':str(uuid.uuid4()),'certifications':[{**details,'certificateId':'registration-'+uuid.uuid4().hex[:8]}]}
 added=good(admin,'/members','POST',payload)
 check('Add-member registration saves certificate atomically',len(good(admin,'/member-certifications?personId='+added['personId']))==1)
 check('Registration retry does not duplicate member or certification',good(admin,'/members','POST',payload)==added and len(good(admin,'/member-certifications?personId='+added['personId']))==1)
 bad={**payload,'requestKey':str(uuid.uuid4()),'person':{'legalName':'QA Invalid '+uuid.uuid4().hex[:8],'displayName':'QA Invalid'},'certifications':[{**details,'programId':str(uuid.uuid4())}]}
 before=int(sql('SELECT count(*) FROM "Memberships"'));check('Invalid certification rolls back whole registration',admin.req('/members','POST',bad)[0]==400 and int(sql('SELECT count(*) FROM "Memberships"'))==before)
 options=good(club,'/activity-options');check('Exactly five activity categories and three activity levels',options['types']==['Service','Leadership','Business','Training','Nari Cell'] and options['levels']==['Club','Region','National'])
 check('Event type catalog uses the five categories',set(t['name'] for t in good(club,'/event-types'))==set(options['types']))
 fy_start=datetime.datetime.fromisoformat(options['financialYearStart']);fy_end=datetime.datetime.fromisoformat(options['financialYearEnd']);now=datetime.datetime.now(datetime.timezone.utc)
 def activity(start,**extra):
  return {'orgId':club_org,'typeId':'00000000-0000-0000-0000-000000000000','title':'QA Activity '+uuid.uuid4().hex[:8],'startsAt':start.isoformat(),'endsAt':(start+datetime.timedelta(hours=2.5)).isoformat(),'registrationStartsAt':(start-datetime.timedelta(days=3)).isoformat(),'registrationEndsAt':start.isoformat(),'timeZone':'Asia/Kolkata','venue':'Community Hall','activityType':'Training','activityLevel':'Club','activityClubId':club_org,'chiefGuest':'Guest Speaker','description':'Recorded activity details','memberParticipants':20,'outsideVolunteers':7,'projectCoordinator':'Training Coordinator','coordinatorPhone':'+919000000001','coordinatorEmail':'coordinator@example.test','meetingCost':1250.50,**extra}
 past=good(club,'/events','POST',activity(now-datetime.timedelta(days=10)))
 check('Past current-year activity records all requested fields',past['lifecycle']=='Completed' and past['totalHours']==2.5 and past['memberParticipants']==20 and past['outsideVolunteers']==7 and past['meetingCost']==1250.50 and not past['registrationEnabled'])
 check('Future current-year event can be scheduled',good(club,'/events','POST',activity(now+datetime.timedelta(days=2)))['lifecycle']=='Scheduled')
 for kind in options['types']:
  check(kind+' category can be posted by Region President',region.req('/events','POST',activity(now-datetime.timedelta(days=2),orgId=rs['organization']['id'],activityType=kind,activityLevel='Region'))[0]==200)
 check('Previous financial year is rejected',club.req('/events','POST',activity(fy_start-datetime.timedelta(days=2)))[0]==400)
 check('Next financial year is rejected',club.req('/events','POST',activity(fy_end+datetime.timedelta(days=2)))[0]==400)
 check('Activity cannot cross financial year end',club.req('/events','POST',activity(fy_end-datetime.timedelta(hours=1)))[0]==400)
 check('Invalid category and negative costs rejected',club.req('/events','POST',activity(now,activityType='Other'))[0]==400 and club.req('/events','POST',activity(now,meetingCost=-10))[0]==400)
 foreign_club=sql('''SELECT "Id" FROM "Orgs" WHERE "Code"='DEMO-R02-D01-Z01-C01' ''')
 check('President cannot post for another region club',club.req('/events','POST',activity(now,activityClubId=foreign_club))[0] in (400,403,404))
 check('Ordinary member cannot post activities',member.req('/events','POST',activity(now))[0]==403)
 submitted=good(club,'/events/'+past['id']+'/status','POST',{'action':'Submit','version':past['version']})
 req=good(admin,'/governance/requests/'+submitted['approvalRequestId'])['request']
 good(admin,'/governance/requests/'+req['id']+'/decisions','POST',{'decision':'Approve','comment':'Isolated historical activity QA','version':req['version']})
 latest=good(club,'/events/'+past['id'])['eventInfo'];posted=good(club,'/events/'+past['id']+'/status','POST',{'action':'Publish','version':latest['version']})
 check('Past activity can pass approval and publish',posted['publication']=='Published' and posted['lifecycle']=='Completed')
 retired=good(admin,'/certifications/'+program['id'],'PUT',{'name':program['name'],'description':program['description'],'active':False,'version':program['version']})
 check('Retiring a program retains member certificates',any(r['certification']['id']==certificate['id'] for r in good(member,'/member-certifications')) and not retired['active'])
 check('New certificates cannot use a retired program',member.req('/member-certifications','POST',{**details,'certificateId':'new-retired'})[0]==400)
 # Smoke checks for hierarchy-dependent modules after conversion.
 for path in ['/structure','/membership/options','/dashboard','/event-hierarchy','/leadership/center','/governance/options','/members','/directory/regions','/finance/entities','/finance/dues-center/options','/structure/intelligence']:
  code,data=admin.req(path);check('Migrated module '+path+' remains available',code==200,(code,data))
 print(str(len(results))+' four-level feature checks passed.',flush=True)
finally:
 process.terminate();process.wait(timeout=20);log.close()
