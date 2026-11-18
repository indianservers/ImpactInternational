"""Role/audience isolation and mocked Google OAuth/upload/stream integration on a disposable DB."""
import datetime,json,os,pathlib,urllib.request,urllib.error,urllib.parse,uuid
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5112'
os.environ['CLUBS_TEST_DATABASE']='clubs_activity_media_qa_20261117'
from support import Client,sql
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_activity_media_qa_')
out=pathlib.Path('docs/qa/activity-media');out.mkdir(parents=True,exist_ok=True)
checks=[]
def check(name,ok):
 checks.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,name
def call(c,path,method='GET',body=None,status=200):
 code,data=c.req(path,method,body);assert code==status,(path,code,data);return data
def login(email):
 c=Client();assert c.login(email)[0]==200,email;return c
def org(code):return json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Code"=\''+code+'\''))
def assign(email,role,target):
 existing=sql("""SELECT a."Id" FROM "Assignments" a JOIN "Users" u ON u."Id"=a."UserId" JOIN "Roles" r ON r."Id"=a."RoleId" WHERE u."Email"='%s' AND r."Name"='%s' AND a."OrgId"='%s' """%(email,role,target))
 if existing:return existing
 ident=str(uuid.uuid4());sql('''INSERT INTO "Assignments" ("Id","UserId","TenantId","RoleId","OrgId") SELECT '%s',u."Id",r."TenantId",r."Id",'%s' FROM "Users" u CROSS JOIN "Roles" r WHERE u."Email"='%s' AND r."Name"='%s' LIMIT 1'''%(ident,target,email,role));return ident
# Test-only clone: use the known seeded member password for fixture officers.
sql("""UPDATE "Users" SET "PasswordHash"=(SELECT "PasswordHash" FROM "Users" WHERE "Email"='member.club1@example.test'),"FailedAttempts"=0,"LockedUntil"=NULL WHERE "Email" IN ('admin@example.test','club.president@example.test')""")
sql('UPDATE "GoogleMediaConnections" SET "Connected"=FALSE,"ActiveForUploads"=FALSE')
sql('UPDATE "GoogleMediaConnections" SET "ActiveForUploads"=false,"Connected"=false')
admin=login('admin@example.test');pres=login('club.president@example.test');member=login('member.club1@example.test');sibling=login('member.club2@example.test');otherregion=login('member.region2@example.test')
club=org('DEMO-R01-D01-Z01-C01');region=org('DEMO-R01');national=org('DEMO-NAT-01')
rp=login('member.zone2@example.test');np=login('member.district2@example.test')
call(rp,'/workspaces/'+assign('member.zone2@example.test','Regional President',region['Id'])+'/activate','POST',{},204)
call(np,'/workspaces/'+assign('member.district2@example.test','National President',national['Id'])+'/activate','POST',{},204)
original=sql('''SELECT a."Id" FROM "Assignments" a JOIN "Users" u ON u."Id"=a."UserId" JOIN "Roles" r ON r."Id"=a."RoleId" WHERE u."Email"='member.region2@example.test' AND r."Name"='Club Member' LIMIT 1''')
call(otherregion,'/workspaces/'+original+'/activate','POST',{},204)
tenant=club['TenantId'];prefix='Media QA '+uuid.uuid4().hex[:6]
def payload(visibility,oid=club['Id']):return {'orgId':oid,'title':prefix+' '+visibility,'body':'An activity with photos, videos and community impact.','category':'Community service','activityOn':str(datetime.date.today()),'visibility':visibility,'requestKey':str(uuid.uuid4())}
posts={v:call(pres,'/community/posts','POST',payload(v)) for v in ['Club','Region','National','Public']}
def ids(c,path='/community/posts'):return {x['id'] for x in call(c,path)['items']}
check('Club president can post all four audience types',len(posts)==4)
check('Own club members see club activity',posts['Club']['id'] in ids(member))
check('Sibling club cannot see club-only activity',posts['Club']['id'] not in ids(sibling))
check('Sibling club sees region audience',posts['Region']['id'] in ids(sibling))
check('Other region cannot see region-only activity',posts['Region']['id'] not in ids(otherregion))
check('Other region within same national sees national audience',posts['National']['id'] in ids(otherregion))
check('Regional president oversees club-only activity',posts['Club']['id'] in ids(rp))
check('National president oversees all descendant activities',all(p['id'] in ids(np) for p in posts.values()))
call(member,'/community/posts','POST',payload('Public'),403);check('Ordinary member cannot publish',True)
call(rp,'/community/posts','POST',payload('Club',region['Id']),400);check('Region post cannot target nonexistent club audience',True)
call(pres,'/community/posts','POST',payload('Region',region['Id']),403);check('Club president cannot post in parent unit',True)
regional=call(rp,'/community/posts','POST',payload('Region',region['Id']));check('Regional president can publish own unit activity',bool(regional['id']))
anon=Client();public='/public/activities/'+tenant
check('Anonymous feed contains only public posts',posts['Public']['id'] in ids(anon,public) and all(posts[v]['id'] not in ids(anon,public) for v in ['Club','Region','National']))
call(member,'/community/posts/'+posts['Public']['id']+'/reaction','PUT',{'liked':True});call(member,'/community/posts/'+posts['Public']['id']+'/comments','POST',{'body':'Great community work!'})
check('Activity reactions and comments still work',len(call(member,'/community/posts/'+posts['Public']['id']+'/comments')['items'])==1)
for path in ['/google-media/accounts','/google-media/connect']:
 call(pres,path,'POST' if path.endswith('connect') else 'GET',{} if path.endswith('connect') else None,403)
check('Google account management limited to Platform Admin',True)
sql('UPDATE "GoogleMediaConnections" SET "Connected"=FALSE,"ActiveForUploads"=FALSE')
call(pres,'/community/posts/'+posts['Public']['id']+'/media/start','POST',{'fileName':'photo.png','mimeType':'image/png','size':20},409)
check('Media upload blocked until admin connects Google',True)
class NoRedirect(urllib.request.HTTPRedirectHandler):
 def redirect_request(self,*args):return None
def redirect(url,client=None):
 opener=urllib.request.build_opener(NoRedirect(),*([urllib.request.HTTPCookieProcessor(client.jar)] if client else []))
 try:
  with opener.open(url) as r:return r.status,dict(r.headers),r.read()
 except urllib.error.HTTPError as e:return e.code,dict(e.headers),e.read()
def consent(client,hint=None):
 data=call(client,'/google-media/connect','POST',{});parsed=urllib.parse.urlsplit(data['url']);q=urllib.parse.parse_qs(parsed.query)
 check('Consent uses state PKCE offline and least privilege Drive scope','code_challenge' in q and q['code_challenge_method']==['S256'] and q['access_type']==['offline'] and 'https://www.googleapis.com/auth/drive.file' in q['scope'][0] and 'photoslibrary' not in q['scope'][0])
 url=data['url']+('&login_hint='+hint if hint else '')
 code,headers,_=redirect(url);assert code==302
 return headers['Location'].replace('http://127.0.0.1:1252','http://127.0.0.1:5112')
callback=consent(admin)
second=login('admin@example.test');check('OAuth state bound to initiating session',redirect(callback,second)[0]==400)
check('OAuth callback connects account',redirect(callback,admin)[0]==302)
check('OAuth callback replay rejected',redirect(callback,admin)[0]==400)
accounts=call(admin,'/google-media/accounts')['accounts'];connection=next(c for c in accounts if c['activeForUploads'])
check('Account list never returns OAuth secrets','fake-' not in json.dumps(accounts) and all('token' not in k.lower() for k in accounts[0]))
encrypted=sql('SELECT "RefreshTokenProtected" FROM "GoogleMediaConnections" WHERE "Id"=\''+connection['id']+'\'')
check('Refresh token encrypted at rest',encrypted and 'fake-refresh' not in encrypted)
raw=pathlib.Path('client/public/brand/impact-community.webp').read_bytes()
upload=call(pres,'/community/posts/'+posts['Public']['id']+'/media/start','POST',{'fileName':'community.webp','mimeType':'image/webp','size':len(raw)})
check('Upload yields scoped session not Google OAuth credentials',upload['uploadUrl'].startswith('http://127.0.0.1:5113/sessions/') and 'access' not in json.dumps(upload))
split=256*1024
req=urllib.request.Request(upload['uploadUrl'],data=raw[:split],method='PUT',headers={'Content-Type':'image/webp','Content-Range':f'bytes 0-{split-1}/{len(raw)}'})
try:urllib.request.urlopen(req);raise AssertionError('Expected resumable 308')
except urllib.error.HTTPError as e:assert e.code==308 and e.headers['Range']==f'bytes=0-{split-1}'
status_request=urllib.request.Request(upload['uploadUrl'],data=b'',method='PUT',headers={'Content-Range':f'bytes */{len(raw)}'})
try:urllib.request.urlopen(status_request);raise AssertionError('Expected incomplete status')
except urllib.error.HTTPError as e:check('Interrupted Google upload reports resumable offset',e.code==308 and e.headers['Range']==f'bytes=0-{split-1}')
req=urllib.request.Request(upload['uploadUrl'],data=raw[split:],method='PUT',headers={'Content-Type':'image/webp','Content-Range':f'bytes {split}-{len(raw)-1}/{len(raw)}'})
with urllib.request.urlopen(req) as r:assert r.status==200
call(pres,'/community/media/'+upload['id']+'/complete','POST',{});call(pres,'/community/media/'+upload['id']+'/complete','POST',{})
check('Upload completion verified and idempotent',True)
code,content,headers=anon.request('/public/activity-media/'+upload['id']+'/content');check('Public photo streams without sign-in',code==200 and content==raw and 'no-store' in headers.get('Cache-Control',''))
code,part,headers=anon.request('/public/activity-media/'+upload['id']+'/content',headers={'Range':'bytes=0-15'});check('Byte range playback supported',code==206 and part==raw[:16] and headers['Content-Range'].startswith('bytes 0-15/'))
rows=call(pres,'/community/posts')['items'];p=next(p for p in rows if p['id']==posts['Public']['id']);p['visibility']='Club'
updated=call(pres,'/community/posts/'+p['id'],'PUT',p)
call(anon,'/public/activity-media/'+upload['id']+'/content',status=404)
call(sibling,'/community/media/'+upload['id']+'/content',status=404)
check('Restricting visibility revokes anonymous and sibling media access',True)
check('Same club still sees restricted media',member.request('/community/media/'+upload['id']+'/content')[0]==200)
call(member,'/community/posts/'+p['id']+'/media/start','POST',{'fileName':'bad.jpg','mimeType':'image/jpeg','size':20},403)
call(pres,'/community/posts/'+p['id']+'/media/start','POST',{'fileName':'payload.html','mimeType':'text/html','size':20},400)
call(pres,'/community/posts/'+p['id']+'/media/start','POST',{'fileName':'big.mp4','mimeType':'video/mp4','size':501*1024*1024},400)
check('Media authorization type and size validated',True)
check('Activity folder stored only as Google reference',bool(sql('SELECT "DriveFolderId" FROM "CommunityPosts" WHERE "Id"=\''+p['id']+'\'')))
# Leave a public fixture for browser QA.
p['version']=updated['version'];p['visibility']='Public';call(pres,'/community/posts/'+p['id'],'PUT',p)
# A second national unit cannot inherit another national audience.
foreign=str(uuid.uuid4())
sql("""INSERT INTO "Orgs" SELECT (jsonb_populate_record(NULL::"Orgs",to_jsonb(o)||jsonb_build_object('Id','%s','Code','QA-NAT-%s','Name','Other national QA'))).* FROM "Orgs" o WHERE o."Id"='%s' """%(foreign,foreign[:8],national['Id']))
other_assignment=assign('member.region2@example.test','National President',foreign)
call(otherregion,'/workspaces/'+other_assignment+'/activate','POST',{},204)
visible=ids(otherregion)
check('Separate national unit excluded from club region and national audiences',all(posts[v]['id'] not in visible for v in ['Club','Region','National']) and posts['Public']['id'] in visible)
foreignpost=call(otherregion,'/community/posts','POST',payload('National',foreign))
check('National president cannot see another national unit private update',foreignpost['id'] not in ids(np))
# Account switching does not move existing activity folders.
callback2=consent(admin,'secondary@example.test');check('Additional Google account connects',redirect(callback2,admin)[0]==302)
second_account=next(c for c in call(admin,'/google-media/accounts')['accounts'] if c['activeForUploads'])
call(admin,'/google-media/accounts/'+connection['id']+'/activate','POST',{},204)
check('Default storage account can be switched',next(c for c in call(admin,'/google-media/accounts')['accounts'] if c['activeForUploads'])['id']==connection['id'])
call(admin,'/google-media/accounts/'+second_account['id']+'/disconnect','POST',{},204)
check('Disconnect revokes an account and retains original activity access',not next(c for c in call(admin,'/google-media/accounts')['accounts'] if c['id']==second_account['id'])['connected'] and member.request('/community/media/'+upload['id']+'/content')[0]==200)
# Expired consent cannot be replayed.
expired=call(admin,'/google-media/connect','POST',{})['url'];state=urllib.parse.parse_qs(urllib.parse.urlsplit(expired).query)['state'][0]
import hashlib
sql('UPDATE "GoogleMediaOAuthStates" SET "ExpiresAt"=now()-interval \'1 minute\' WHERE "StateHash"=\''+hashlib.sha256(state.encode()).hexdigest().upper()+'\'')
check('Expired OAuth state rejected',redirect('http://127.0.0.1:5112/api/v1/google-media/callback?state='+state+'&code=ignored',admin)[0]==400)
report=call(np,'/community/report');check('National report includes descendant club updates',any(r['id']==p['id'] for r in report['recent']))
(out/'api-checks.json').write_text(json.dumps(checks,indent=2))
(out/'fixture.json').write_text(json.dumps({'postId':p['id'],'mediaId':upload['id'],'tenantId':tenant,'prefix':prefix}))
print('Passed '+str(len(checks))+' activity/media checks')
