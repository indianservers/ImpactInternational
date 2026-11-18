"""All ten community enhancements, authorized scope, consent and approval regression on an isolated database."""
import base64,datetime,hashlib,json,os,pathlib,time,urllib.request,urllib.error,urllib.parse,uuid
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5112';os.environ['CLUBS_TEST_DATABASE']='clubs_community_growth_qa_20261118'
from support import Client,sql
assert os.environ['CLUBS_TEST_DATABASE']=='clubs_community_growth_qa_20261118'
out=pathlib.Path('docs/qa/community-growth');out.mkdir(parents=True,exist_ok=True);checks=[];prefix='Growth '+uuid.uuid4().hex[:6]
def check(name,ok):
 checks.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,name
def call(c,path,method='GET',body=None,status=200):
 code,data=c.req(path,method,body);assert code==status,(path,code,data);return data
def login(email):
 c=Client();assert c.login(email)[0]==200,email;return c
def unit(code):return json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Code"=\''+code+'\''))
def activate(c,email,role):
 aid=sql('''SELECT a."Id" FROM "Assignments" a JOIN "Users" u ON u."Id"=a."UserId" JOIN "Roles" r ON r."Id"=a."RoleId" WHERE u."Email"='%s' AND r."Name"='%s' ORDER BY a."Id" LIMIT 1'''%(email,role));assert aid;call(c,'/workspaces/'+aid+'/activate','POST',{},204)
def person(email):return sql('SELECT p."Id" FROM "Persons" p JOIN "Users" u ON u."Id"=p."UserId" WHERE u."Email"=\''+email+'\'')
sql("UPDATE \"BusinessOpportunities\" SET \"CreatedAt\"=now()-interval '2 hours' WHERE \"Title\" LIKE 'Growth %'")
admin=login('admin@example.test');pres=login('club.president@example.test');alice=login('member.club1@example.test');bob=login('member.club2@example.test');regional=login('member.zone2@example.test');national=login('member.district2@example.test');anonymous=Client()
activate(regional,'member.zone2@example.test','Regional President');activate(national,'member.district2@example.test','National President');activate(alice,'member.club1@example.test','Club Member');activate(bob,'member.club2@example.test','Club Member')
club=unit('DEMO-R01-D01-Z01-C01');tenant=club['TenantId'];alice_id=person('member.club1@example.test');bob_id=person('member.club2@example.test');today=datetime.date.fromisoformat(call(pres,'/community/spaces')['today'])
policy=call(admin,'/community/policy');call(admin,'/community/policy','PUT',{'requirePublicApproval':True,'version':policy['version']});check('Platform Admin can enable public approval',True)
call(pres,'/community/policy','PUT',{'requirePublicApproval':False,'version':1},403)
def body(visibility='Public',**extra):return {'orgId':club['Id'],'title':prefix+' activity','body':'A meaningful community service activity with verified participation.','category':'Community service','activityOn':str(today),'visibility':visibility,'requestKey':str(uuid.uuid4()),'latitude':17.385,'longitude':78.4867,**extra}
p=call(pres,'/community/posts','POST',body(participantIds=[alice_id]));pid=p['id'];check('Public post enters parent-unit review',p['status']=='Pending')
check('Pending post absent from anonymous feed',pid not in {x['id'] for x in call(anonymous,'/public/activities/'+tenant)['items']})
call(alice,'/community/posts/'+pid,status=404);check('Pending post hidden from ordinary members',True)
check('Author can find own pending activity',any(x['id']==pid for x in call(pres,'/community/posts?search='+urllib.parse.quote(prefix))['items']))
check('Regional president receives club review',any(x['id']==pid for x in call(regional,'/community/review')))
call(pres,'/community/posts/'+pid+'/review','POST',{'decision':'Approve','version':p['version']},403)
review=call(regional,'/community/posts/'+pid+'/review','POST',{'decision':'Approve','version':p['version'],'note':'Reviewed the activity summary and consent process.'})
check('Parent review publishes the post',review['status']=='Published')
call(regional,'/community/posts/'+pid+'/review','POST',{'decision':'Approve','version':p['version']},409);check('Stale or duplicate review rejected',True)
post=call(alice,'/community/posts/'+pid);check('Accepted public post visible in feed',post['status']=='Published')
tag=next(t for t in call(alice,'/community/participation/me')['items'] if t['postId']==pid)
call(bob,'/community/participation/'+tag['id']+'/consent','POST',{'decision':'Accepted','version':tag['version']},404)
call(pres,'/community/participation/'+tag['id']+'/certificate','POST',{},409);check('Tagging alone cannot issue a certificate',True)
call(alice,'/community/participation/'+tag['id']+'/consent','POST',{'decision':'Accepted','publicName':False,'version':tag['version']})
tag=next(t for t in call(pres,'/community/posts/'+pid+'/participants') if t['personId']==alice_id)
call(pres,'/community/participation/'+tag['id']+'/verify','POST',{'verified':True,'evidence':'Signed attendance register checked by club president.','role':'Volunteer','hours':2.5,'version':tag['version']})
certificate=call(pres,'/community/participation/'+tag['id']+'/certificate','POST',{});again=call(pres,'/community/participation/'+tag['id']+'/certificate','POST',{})
check('Verified accepted participation issues certificate idempotently',certificate['id']==again['id'])
mine=call(alice,'/community/participation/me');cert=next(c for c in mine['certificates'] if c['id']==certificate['id']);token=cert['verificationUrl'].split('/')[-1]
verified=call(anonymous,'/public/activity-certificates/'+token);check('Public verification respects withheld recipient name',verified['status']=='Valid' and verified['recipient'] is None)
check('Public activity hides participant names without public-name consent',len(call(anonymous,'/public/activities/'+tenant+'?postId='+pid)['items'][0]['taggedMembers'])==0)
code,pdf,_=alice.request('/community/certificates/'+certificate['id']+'/download');check('Participant downloads generated certificate PDF',code==200 and pdf.startswith(b'%PDF') and len(pdf)>2000);(out/'sample-certificate.pdf').write_bytes(pdf)
call(bob,'/community/certificates/'+certificate['id']+'/download',status=403)
check('Verified contributions earn first milestone',mine['milestones'][0]['earned'] and mine['verifiedHours']>=2.5)
history=call(alice,'/community/people/'+alice_id+'/participation');check('Accepted participation appears in member history',any(t['postId']==pid and t['verified'] for t in history))
tag=next(t for t in mine['items'] if t['postId']==pid);call(alice,'/community/participation/'+tag['id']+'/consent','POST',{'decision':'Declined','version':tag['version']})
check('Withdrawing consent revokes public certificate validity',call(anonymous,'/public/activity-certificates/'+token)['status']=='Revoked')
# Restore a useful recognition fixture.
tag=next(t for t in call(alice,'/community/participation/me')['items'] if t['postId']==pid);call(alice,'/community/participation/'+tag['id']+'/consent','POST',{'decision':'Accepted','publicName':True,'version':tag['version']});call(pres,'/community/participation/'+tag['id']+'/certificate','POST',{})
post=call(pres,'/community/posts/'+pid);post['title']+=' edited';edited=call(pres,'/community/posts/'+pid,'PUT',post);check('Editing approved public content requires review again',edited['status']=='Pending')
call(regional,'/community/posts/'+pid+'/review','POST',{'decision':'Reject','version':edited['version'],'note':'Please expand the outcomes and learning points.'});check('Reviewer can request changes with reason',True)
post['version']=edited['version']+1;post['body']+=' Members learned collaboration skills and supported the local community.';resubmitted=call(pres,'/community/posts/'+pid,'PUT',post);check('Rejected update can be edited and resubmitted',resubmitted['status']=='Pending');call(regional,'/community/posts/'+pid+'/review','POST',{'decision':'Approve','version':resubmitted['version']})
internal=call(pres,'/community/posts','POST',body('Club',title=prefix+' internal'));check('Internal updates publish immediately',internal['status']=='Published')
# Business matching uses only opt-in active professional profiles.
for client,cat,tags in [(alice,'Technology',['Automation']),(bob,'Technology',['Automation','Web apps'])]:
 profile=call(client,'/networking/me');profile.update(company='QA Digital Studio',businessCategory=cat,businessSubcategory='Software development',businessTags=tags,city='Hyderabad',visible=True);call(client,'/networking/me','PUT',profile)
op=call(alice,'/opportunities','POST',{'title':prefix+' needs a website','body':'Looking for a member who can build a reliable business website.','category':'Technology','subcategory':'Software development','tags':['Automation','Web apps'],'city':'Hyderabad','closesOn':str(today+datetime.timedelta(days=30))})
matches=call(bob,'/opportunities?matches=true');check('Business requirements match member business categories and tags',any(o['id']==op['id'] for o in matches['items']))
recs=call(alice,'/community/recommendations?opportunityId='+op['id']);check('Member recommendations include clear matching reasons',any(r['id']==bob_id and r['score']>0 and len(r['reasons'])>=2 for r in recs))
profile=call(bob,'/networking/me');profile['visible']=False;call(bob,'/networking/me','PUT',profile);check('Hidden professional profiles excluded from recommendations',all(r['id']!=bob_id for r in call(alice,'/community/recommendations?opportunityId='+op['id'])))
profile=call(bob,'/networking/me');profile['visible']=True;call(bob,'/networking/me','PUT',profile)
call(bob,'/opportunities/'+op['id']+'/respond','POST',{'message':'Our software studio can help with your business website.'},204);call(bob,'/opportunities/'+op['id']+'/respond','POST',{'message':'Duplicate response.'},409)
check('Opportunity owner sees private responses',len(call(alice,'/opportunities/'+op['id']+'/responses'))==1);call(pres,'/opportunities/'+op['id']+'/responses',status=403)
# Following is not a new visibility grant. Digests are explicitly opt-in.
call(alice,'/community/following','PUT',{'kind':'Topic','target':'Community service','following':True},204);check('Members can follow activity topics',any(f['target']=='Community service' for f in call(alice,'/community/following')['follows']))
preview=call(alice,'/community/digest/preview');check('Live personalized digest includes followed visible posts',any(x['id']==pid for x in preview['items']))
call(bob,'/community/following','PUT',{'kind':'Organization','target':club['Id'],'following':True},204);check('Following a club does not reveal club-only posts',all(x['id']!=internal['id'] for x in call(bob,'/community/discover?following=true')['items']))
call(alice,'/community/digest','PUT',{'enabled':True},204);check('Weekly digest preference persists',call(alice,'/community/following')['digest']['enabled'])
digest_before=int(sql('SELECT count(*) FROM "Mails" WHERE "Kind"=\'community.digest\' AND "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member.club1@example.test\')'))
sql('UPDATE "CommunityDigestPreferences" SET "NextDigestAt"=now()-interval \'1 minute\' WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member.club1@example.test\')')
region_id=unit('DEMO-R01')['Id']
check('Regional discovery includes permitted descendant club activities',any(x['id']==internal['id'] for x in call(alice,'/community/discover?orgId='+region_id)['items']))
# Discovery and leadership scope are applied before points and aggregates.
discovery=call(alice,'/community/discover?category=Community%20service');check('Map contains only permitted activity coordinates',any(x['id']==pid and x['latitude']==17.385 for x in discovery['points']))
other_map=call(bob,'/community/discover');check('Map hides private activities from other clubs',all(x['id']!=internal['id'] for x in other_map['points']))
impact=call(national,'/community/impact');check('Leadership impact includes verified participants and reporting coverage',impact['summary']['uniqueVerifiedMembers']>=1 and 'silentClubs' in impact['summary']);call(alice,'/community/impact',status=403)
# Completed event draft copies checked-in attendance, fields and tagged members.
eid=str(uuid.uuid4());rid=str(uuid.uuid4());checkin=str(uuid.uuid4());operator=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'club.president@example.test\'')
sql('''INSERT INTO "Events" SELECT (jsonb_populate_record(NULL::"Events",to_jsonb(e)||jsonb_build_object('Id','%s','OrgId','%s','CreatorId','%s','Title','%s event','Description','A completed community event with verified attendance.','Approval','Approved','Publication','Published','Lifecycle','Scheduled','StartsAt',now()-interval '2 days','EndsAt',now()-interval '1 day','RegistrationStartsAt',now()-interval '4 days','RegistrationEndsAt',now()-interval '3 days','RsvpDeadline',now()-interval '3 days','TimeZone','Asia/Kolkata','Venue','Hyderabad','Latitude',17.385,'Longitude',78.4867))).* FROM "Events" e LIMIT 1'''%(eid,club['Id'],operator,prefix))
reg={'Id':rid,'TenantId':tenant,'EventId':eid,'PersonId':alice_id,'ParticipantKey':'person:'+alice_id,'Name':'QA participant','Email':'member.club1@example.test','Category':'Member','Status':'Confirmed','Companions':0,'GuestNames':'[]','Responses':'{}','Accessibility':'','Dietary':'','Emergency':'','CreatedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'FeedbackSubmitted':False,'Version':1}
sql("UPDATE \"Events\" SET \"Lifecycle\"='Scheduled' WHERE \"Id\"='"+eid+"'; "+'INSERT INTO \"EventRegistrations\" SELECT (jsonb_populate_record(NULL::\"EventRegistrations\",\''+json.dumps(reg).replace("'","''")+'\'::jsonb)).*; '+"UPDATE \"Events\" SET \"Lifecycle\"='Completed' WHERE \"Id\"='"+eid+"'")
sql('''INSERT INTO "EventCheckins" ("Id","TenantId","EventId","RegistrationId","OperatorId","Device","EntryPoint","At","Voided","CorrectionReason") VALUES ('%s','%s','%s','%s','%s','QA','Main entrance',now()-interval '1 day',false,'')'''%(checkin,tenant,eid,rid,operator))
draft=call(pres,'/community/events/'+eid+'/draft');check('Event draft copies venue coordinates and checked-in members',draft['participants']==1 and alice_id in draft['participantIds'] and draft['location']=='Hyderabad')
event_post=call(pres,'/community/posts','POST',body('Club',**{k:v for k,v in draft.items() if k!='visibility'}));call(pres,'/community/posts','POST',body('Club',**draft),409);check('An event creates only one activity post',True)
# Existing event gallery images import into the activity's Google folder once.
image=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=')
docid=str(uuid.uuid4());doc={'Id':docid,'TenantId':tenant,'OrgId':club['Id'],'EventId':eid,'Kind':'Gallery','Title':'QA event memory','FileName':'event-memory.png','ContentType':'image/png','Content':'\\x'+image.hex(),'Sha256':hashlib.sha256(image).hexdigest(),'UploaderId':operator,'ShareWithSuccessors':False,'At':datetime.datetime.now(datetime.timezone.utc).isoformat(),'Status':'Available','CurrentVersion':1,'Folder':'General','ObjectKey':'','SharedUsers':'[]','Tags':'','Visibility':'Organization'}
sql('INSERT INTO "GovernanceDocuments" SELECT (jsonb_populate_record(NULL::"GovernanceDocuments",\''+json.dumps(doc).replace("'","''")+'\'::jsonb)).*')
imported=call(pres,'/community/posts/'+event_post['id']+'/import-event-media','POST',{})
check('Event gallery copied to Google without reuploading local files',imported['imported']==1)
check('Repeating gallery import does not duplicate photos',call(pres,'/community/posts/'+event_post['id']+'/import-event-media','POST',{})['imported']==0)
upload=call(pres,'/community/posts/'+event_post['id']+'/media/start','POST',{'fileName':'retry.png','mimeType':'image/png','size':len(image)})
call(pres,'/community/media/'+upload['id']+'/failure','POST',{},204)
check('Failed upload appears in storage health',any(m['id']==upload['id'] and m['status']=='Failed' for m in call(admin,'/google-media/health')['issues']))
retry=call(pres,'/community/media/'+upload['id']+'/retry','POST',{})
req=urllib.request.Request(retry['uploadUrl'],data=image,method='PUT',headers={'Content-Type':'image/png','Content-Range':f'bytes 0-{len(image)-1}/{len(image)}'})
with urllib.request.urlopen(req) as response:assert response.status==200
call(pres,'/community/media/'+upload['id']+'/complete','POST',{})
check('Storage retry recovers an interrupted upload',all(m['id']!=upload['id'] for m in call(admin,'/google-media/health')['issues']))
# Wait for the durable worker once. It never sends externally because SMTP is disabled.
queued=False
for _ in range(80):
 queued=int(sql('SELECT count(*) FROM "Mails" WHERE "Kind"=\'community.digest\' AND "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member.club1@example.test\')'))>digest_before
 if queued:break
 time.sleep(.5)
check('Opt-in weekly digest is queued by the background worker',queued)
call(alice,'/community/digest','PUT',{'enabled':False},204);check('Opting out suppresses pending digest delivery',sql('SELECT count(*) FROM "Mails" WHERE "Kind"=\'community.digest\' AND NOT "Suppressed" AND "SentAt" IS NULL AND "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member.club1@example.test\')')=='0')
check('Storage health available to Platform Admin', 'issues' in call(admin,'/google-media/health'));call(pres,'/google-media/health',status=403)
# Retain revoked certificate history while removing a participation tag.
removal=call(pres,'/community/posts','POST',body('Club',title=prefix+' removable participation',participantIds=[alice_id]))
rt=next(t for t in call(pres,'/community/posts/'+removal['id']+'/participants') if t['personId']==alice_id)
call(alice,'/community/participation/'+rt['id']+'/consent','POST',{'decision':'Accepted','publicName':False,'version':rt['version']})
rt=next(t for t in call(pres,'/community/posts/'+removal['id']+'/participants') if t['personId']==alice_id)
call(pres,'/community/participation/'+rt['id']+'/verify','POST',{'verified':True,'evidence':'Attendance register checked for removal regression.','version':rt['version']})
rc=call(pres,'/community/participation/'+rt['id']+'/certificate','POST',{})
call(pres,'/community/certificates/'+rc['id']+'/revoke','POST',{'reason':'Certificate revoked before removing the participation tag.'},204)
rpost=call(pres,'/community/posts/'+removal['id']);rpost['participantIds']=[];call(pres,'/community/posts/'+removal['id'],'PUT',rpost)
call(alice,'/community/participation/'+rt['id']+'/consent','POST',{'decision':'Accepted','version':rt['version']},404)
check('Removed participation cannot be restored by replaying member consent',True)
check('Removing a revoked participant preserves certificate audit history',len(call(pres,'/community/posts/'+removal['id']+'/participants'))==0 and sql('SELECT count(*) FROM "ActivityCertificates" WHERE "Id"=\''+rc['id']+'\'')=='1')
expired=call(alice,'/opportunities','POST',{'title':prefix+' expired requirement','body':'This requirement has closed and is retained for owner history.','category':'Technology','subcategory':'','tags':[],'city':'','closesOn':str(today-datetime.timedelta(days=1)),'status':'Closed'})
check('Expired requirements can be retained as closed',bool(expired['id']))
call(pres,'/community/posts','POST',body('Club',latitude=17.0,longitude=None),400);check('Partial map coordinates are rejected',True)
call(pres,'/community/posts','POST',body('Club',participantIds=[bob_id]),400);check('Cannot tag members from another club outside posting unit',True)
policy=call(admin,'/community/policy');call(admin,'/community/policy','PUT',{'requirePublicApproval':False,'version':policy['version']});direct=call(pres,'/community/posts','POST',body('Public',title=prefix+' policy-off public'))
check('Optional approval policy can allow direct public publication',direct['status']=='Published')
policy=call(admin,'/community/policy');call(admin,'/community/policy','PUT',{'requirePublicApproval':True,'version':policy['version']})
(out/'api-checks.json').write_text(json.dumps(checks,indent=2));(out/'fixture.json').write_text(json.dumps({'postId':pid,'opportunityId':op['id'],'eventId':eid,'eventPostId':event_post['id'],'tenantId':tenant,'prefix':prefix}))
print('Passed '+str(len(checks))+' community growth checks')
