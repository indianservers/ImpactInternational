"""Networking consent, introductions and organization-scoped community/report integration.
Only the isolated QA database is modified.
"""
import datetime,io,csv,json,os,pathlib,sys,urllib.parse,uuid
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5101'
os.environ['CLUBS_TEST_DATABASE']='clubs_approval_scope_20261114'
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from support import Client,sql,BASE,PASSWORD
assert BASE=='http://127.0.0.1:5101'
out=pathlib.Path('docs/qa/networking-community');out.mkdir(parents=True,exist_ok=True)
checks=[];prefix=uuid.uuid4().hex[:8]
def check(name,ok,detail=None):
 checks.append(dict(name=name,passed=bool(ok),detail=detail));(out/'results.json').write_text(json.dumps(dict(total=len(checks),passed=sum(c['passed'] for c in checks),checks=checks),indent=2),encoding='utf-8');assert ok,(name,detail)
def call(c,path,method='GET',body=None,expected=200):
 status,data=c.req(path,method,body);check(method+' '+path,status==expected,None if status==expected else dict(status=status,error=data));return data
def login(email):
 c=Client();check('Login '+email,c.login(email)[0]==200);return c
def org(code):return json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Code"=\''+code+'\''))
admin=login('saisatish@indianservers.com')
units={code:org(code) for code in ['IMPACT-HQ','DEMO-NAT-01','DEMO-R01-D01','DEMO-R01-D01-Z01','DEMO-R01-D01-Z01-C01','DEMO-R01-D01-Z01-C02','DEMO-R01-D01-Z02-C01','DEMO-R01-D02-Z01-C01','DEMO-R02-D01-Z01-C01']}
club1=units['DEMO-R01-D01-Z01-C01'];club2=units['DEMO-R01-D01-Z01-C02'];farclub=units['DEMO-R02-D01-Z01-C01']
public=Client()
def member(label,target,approve=True):
 email='network.'+prefix+'.'+label.lower()+'@example.test';zone=json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Id"=\''+target['ParentId']+'\''))
 call(public,'/public/membership-signup','POST',dict(name='Networking QA '+prefix+' '+label,email=email,phone='+919876543210',password=PASSWORD,confirmation=PASSWORD,agreed=True,districtId=zone['ParentId'],zoneId=zone['Id'],clubId=target['Id']),202)
 user=sql('SELECT "Id" FROM "Users" WHERE "Email"=\''+email+'\'');app=json.loads(sql('SELECT row_to_json(a) FROM "MembershipApplications" a WHERE "ApplicantUserId"=\''+user+'\''))
 link=sql('SELECT "Link" FROM "Mails" WHERE "UserId"=\''+user+'\' AND "Kind"=\'verify\' ORDER BY "ExpiresAt" DESC LIMIT 1')
 call(public,'/auth/complete','POST',dict(token=urllib.parse.parse_qs(urllib.parse.urlsplit(link).query)['token'][0],password=''))
 if approve:call(admin,'/membership/applications/'+app['Id']+'/decisions','POST',dict(decision='Approve',comment='Isolated networking QA',version=app['Version']))
 return login(email),app['PersonId'],user,email
a,aid,auid,aemail=member('Alice',club1);b,bid,buid,bemail=member('Bob',club2);hidden,hid,huid,hemail=member('Hidden',farclub);pending,_,_,_=member('Pending',club1,False)
anonymous=Client();call(anonymous,'/networking/people',expected=401);call(pending,'/networking/me',expected=403);call(pending,'/community/posts',expected=403)
def profile(c,**extra):
 current=call(c,'/networking/me');data={k:current[k] for k in ['company','designation','industry','profession','businessCategory','workLocation','website','city','state','country','networking','skills','interests','expertise','visible','allowRequests','version','privacyVersion']}
 data.update(extra);call(c,'/networking/me','PUT',data);return data
profile(a,company='QA Sales '+prefix,designation='Business owner',industry='Software',profession='Consultant',businessCategory='Business services',workLocation='Hitech City',city='Hyderabad',state='Telangana',country='India',skills=['Sales','Public speaking','Sales'],interests=['Entrepreneurship'],expertise=['Mentoring'],networking='Networking QA '+prefix+' business collaboration',visible=True)
profile(b,company='QA Training '+prefix,designation='Trainer',industry='Training',profession='Public speaking coach',businessCategory='Learning',workLocation='Banjara Hills',city='Hyderabad',state='Telangana',country='India',skills=['Public speaking','Sales'],interests=['Entrepreneurship','Public speaking'],networking='Networking QA '+prefix+' speaking practice',visible=True)
profile(hidden,company='Private Canary '+prefix,industry='Hidden industry '+prefix,skills=['Hidden skill '+prefix],interests=['Private interest '+prefix],visible=False)
mine=call(a,'/networking/me');check('Skill tags deduplicate without overwriting contacts',len(mine['skills'])==2 and sql('SELECT count(*) FROM "PersonContacts" WHERE "PersonId"=\''+aid+'\' AND "Value"=\''+aemail+'\'')=='1')
discovery=call(a,'/networking/people?search='+prefix);check('Cross-club opted-in member discovered and self excluded',any(p['id']==bid for p in discovery['items']) and all(p['id']!=aid and p['id']!=hid for p in discovery['items']))
bob=next(p for p in discovery['items'] if p['id']==bid);check('Shared skills and interests counted',bob['shared']==3)
for key,value in [('skill','public speaking'),('interest','Entrepreneurship'),('city','Hyderabad'),('area','Banjara'),('industry','Training')]:
 rows=call(a,'/networking/people?'+urllib.parse.urlencode({'search':prefix,key:value}))['items'];check(key+' filter finds expected member',any(p['id']==bid for p in rows))
detail=call(a,'/networking/people/'+bid);check('Private email and phone stay private across clubs',detail['email'] is None and detail['phone'] is None and bemail not in json.dumps(detail))
check('Professional profile excludes private identity fields',not any(k in detail for k in ['birthDate','addresses','contacts','spouseName','postalCode']))
call(admin,'/networking/people/'+hid,expected=404)
options=call(a,'/networking/options');check('Hidden tags and location data are excluded from options',not any('Hidden skill '+prefix==v for v in options['skills']) and 'Hidden industry '+prefix not in options['industries'])
call(a,'/networking/people/'+aid+'/connect','POST',{'message':'Self introduction forbidden'},404)
call(hidden,'/networking/people/'+bid+'/connect','POST',{'message':'Publish profile first'},403)
request=call(a,'/networking/people/'+bid+'/connect','POST',{'message':'We share speaking and sales interests. Let us connect.'})
call(a,'/networking/people/'+bid+'/connect','POST',{'message':'Duplicate request'},409)
call(b,'/networking/people/'+aid+'/connect','POST',{'message':'Reciprocal duplicate'},409)
incoming=call(b,'/networking/connections?view=incoming');check('Recipient sees pending request and sender profile',any(r['id']==request['id'] and r['profile']['id']==aid and r['canRespond'] for r in incoming['items']))
call(a,'/networking/connections/'+request['id']+'/respond','POST',{'status':'Accepted','reply':''},404)
call(hidden,'/networking/connections/'+request['id']+'/respond','POST',{'status':'Accepted','reply':''},404)
call(b,'/networking/connections/'+request['id']+'/respond','POST',{'status':'Accepted','reply':'Happy to connect.'},204)
call(b,'/networking/connections/'+request['id']+'/respond','POST',{'status':'Accepted','reply':''},409)
accepted=call(a,'/networking/connections?view=accepted');check('Accepted connection visible to sender',any(r['id']==request['id'] and r['status']=='Accepted' for r in accepted['items']))
call(a,'/networking/people/'+bid+'/connect','POST',{'message':'Already connected'},409)
check('Acceptance does not silently reveal private contacts',call(a,'/networking/people/'+bid)['email'] is None)
stale=profile(a,visible=False);call(b,'/networking/people/'+aid,expected=404)
check('Opt-out preserves basic directory preference',sql('SELECT "DirectoryVisible" FROM "PersonPrivacy" WHERE "PersonId"=\''+aid+'\'')=='t')
call(a,'/networking/me','PUT',stale,409);profile(a,visible=True)
bad=call(a,'/networking/me');bad['website']='javascript:alert(1)';call(a,'/networking/me','PUT',bad,400)

leaders={role:login(role+'.president@example.test') for role in ['club','zonal','district','national','international']}
baselines={role:call(c,'/community/report')['summary'] for role,c in leaders.items()}
spaces=call(leaders['club'],'/community/spaces');today=spaces['today']
check('Club administrator can publish only in its own unit',all(s['id']==club1['Id'] for s in spaces['spaces'] if s['canPublish']))
check('Member can read spaces but has no publishing or reporting authority',not call(a,'/community/spaces')['canReport'] and not any(s['canPublish'] for s in call(a,'/community/spaces')['spaces']))
def body(target,title):return dict(orgId=target['Id'],title='QA '+prefix+' '+title,body='Isolated community activity: outcomes, public speaking and teamwork.',category='Training',activityOn=today,participants=10,volunteerHours=2,location='Hyderabad',linkUrl='',pinned=False,requestKey=uuid.uuid4().hex,version=1)
posts={};inputs={}
for name,client,target in [('club1',leaders['club'],club1),('club2',admin,club2),('zone',leaders['zonal'],units['DEMO-R01-D01-Z01']),('district',leaders['district'],units['DEMO-R01-D01']),('national',leaders['national'],units['DEMO-NAT-01']),('otherZone',admin,units['DEMO-R01-D01-Z02-C01']),('otherDistrict',admin,units['DEMO-R01-D02-Z01-C01']),('otherRegion',admin,units['DEMO-R02-D01-Z01-C01']),('international',leaders['international'],units['IMPACT-HQ'])]:
 inputs[name]=body(target,name);posts[name]=call(client,'/community/posts','POST',inputs[name])['id']
again=call(leaders['club'],'/community/posts','POST',inputs['club1']);check('Publication retries reuse the same update',again['id']==posts['club1'])
call(leaders['club'],'/community/posts','POST',{**inputs['club1'],'body':'Different details on same submission key'},409)
call(a,'/community/posts','POST',body(club1,'member cannot publish'),403)
call(leaders['zonal'],'/community/posts','POST',body(club1,'zone cannot impersonate club admin'),403)
call(leaders['club'],'/community/posts','POST',body(units['DEMO-R01-D01-Z01'],'club cannot publish in zone'),403)
call(leaders['club'],'/community/posts','POST',{**body(club1,'bad category'),'category':'Invalid'},400)
feed=call(a,'/community/posts?search='+prefix)['items'];ids={r['id'] for r in feed}
check('Member sees own club and parent announcements only',{posts[k] for k in ['club1','zone','district','national','international']}<=ids and not {posts[k] for k in ['club2','otherZone','otherDistrict','otherRegion']} & ids)
call(a,'/community/posts?orgId='+club2['Id'],expected=404)
call(a,'/community/posts/'+posts['club2']+'/comments',expected=404)
call(b,'/community/posts/'+posts['club1']+'/reaction','PUT',{'liked':True},404)
for _ in range(2):call(a,'/community/posts/'+posts['club1']+'/reaction','PUT',{'liked':True})
liked=next(r for r in call(a,'/community/posts?search='+prefix)['items'] if r['id']==posts['club1']);check('Like action is idempotent',liked['likes']==1 and liked['liked'])
comment=call(a,'/community/posts/'+posts['club1']+'/comments','POST',{'body':'Thank you for sharing this activity.'})
othercomment=call(leaders['zonal'],'/community/posts/'+posts['club1']+'/comments','POST',{'body':'Great club participation and teamwork.'})
call(a,'/community/comments/'+othercomment['id']+'/hide','POST',{},403)
call(leaders['zonal'],'/community/comments/'+comment['id']+'/hide','POST',{},204)
comments=call(a,'/community/posts/'+posts['club1']+'/comments');check('Moderated comments disappear from member view',all(c['id']!=comment['id'] for c in comments['items']))
for role,count in [('club',1),('zonal',3),('district',5),('national',8),('international',9)]:
 report=call(leaders[role],'/community/report');baseline=baselines[role]
 check(role+' report covers correct descendant units',report['summary']['updates']-baseline['updates']==count,report['summary'])
 check(role+' attendance and volunteer totals match included updates',report['summary']['participants']-baseline['participants']==count*10 and report['summary']['volunteerHours']-baseline['volunteerHours']==count*2)
call(a,'/community/report',expected=403)
call(leaders['club'],'/community/report?orgId='+club2['Id'],expected=404)
call(leaders['zonal'],'/community/report?orgId='+units['DEMO-R01-D01-Z02-C01']['Id'],expected=404)
call(leaders['club'],'/community/report?from=2020-01-01&to=2026-01-01',expected=400)
status,data,headers=leaders['club'].request('/community/report?format=csv');csvrows=list(csv.DictReader(io.StringIO(data.decode('utf-8-sig'))));check('Report export includes own scope only',status==200 and all(r['Organization']==club1['Name'] for r in csvrows))
edited=call(leaders['club'],'/community/posts/'+posts['club1'],'PUT',{**inputs['club1'],'title':'QA '+prefix+' updated workshop'})
call(leaders['club'],'/community/posts/'+posts['club1'],'PUT',inputs['club1'],409)
call(leaders['club'],'/community/posts/'+posts['club1']+'/archive','POST',{'version':edited['version'],'reason':'End of isolated community QA fixture'},204)
check('Archived updates leave feed and reports',not any(r['id']==posts['club1'] for r in call(a,'/community/posts?search='+prefix)['items']) and call(leaders['club'],'/community/report')['summary']['updates']==baselines['club']['updates'])
call(pending,'/community/report',expected=403)
check('Networking and community changes have audit records',all(int(sql('SELECT count(*) FROM "Audits" WHERE "Action"=\''+action+'\''))>0 for action in ['networking.profile.updated','networking.introduction.sent','networking.introduction.accepted','community.post.published','community.post.updated','community.post.archived','community.comment.added','community.comment.hidden','community.report.export']))
print(json.dumps(dict(total=len(checks),passed=sum(c['passed'] for c in checks)),indent=2))
