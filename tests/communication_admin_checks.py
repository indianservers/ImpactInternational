"""Actual adapter HTTP/SMTP protocols against local fakes; never contacts real members/providers."""
import os,time,json,uuid,threading,subprocess,pathlib,urllib.parse,hmac,hashlib,base64,email as email_parser
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
import support,concurrent.futures,statistics
DB='clubs_communication_admin_qa_20261115';support.BASE='http://127.0.0.1:5084'
from support import Client
sql=lambda q:support.sql(q,DB)
results=[];calls=[];failures={};receipts=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/communication-admin-qa.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,b=c.req('/communications'+p,m,d);assert code==status,(p,code,b);return b
def until(fn,timeout=30):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.15)
 raise AssertionError('Timed out waiting for persisted provider state')
class Mock(BaseHTTPRequestHandler):
 def log_message(self,*a):pass
 def answer(self,status,data):
  self.send_response(status);self.send_header('Content-Type','application/json');self.end_headers();self.wfile.write(json.dumps(data).encode())
 def do_GET(self):
  if self.path=='/media-download':
   data=b'%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF';self.send_response(200);self.send_header('Content-Type','application/pdf');self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data);return
  if self.path=='/meta/inbound-pdf':return self.answer(200,{'url':'http://127.0.0.1:5085/media-download','mime_type':'application/pdf'})
  if self.path=='/meta/untrusted-media':return self.answer(200,{'url':'http://169.254.169.254/latest/meta-data/','mime_type':'application/pdf'})
  if 'message_templates' in self.path:return self.answer(200,{'data':[{'name':'community_hello','language':'en','status':'APPROVED','components':[{'type':'BODY','text':'Hello {{1}}, welcome to our community.'}]}]})
  if self.path.startswith('/meta/'):return self.answer(200,{'display_phone_number':'+919999999999','verified_name':'Fictional test sender'})
  return self.answer(200,{'incoming_phone_numbers':[{'phone_number':'+18885550000','sms_url':'https://callback.example.test/twilio'}]})
 def do_POST(self):
  if self.headers.get('Transfer-Encoding','').lower()=='chunked':
   chunks=[]
   while True:
    size=int(self.rfile.readline().split(b';')[0].strip(),16)
    if not size:self.rfile.readline();break
    chunks.append(self.rfile.read(size));self.rfile.read(2)
   raw=b''.join(chunks)
  else:raw=self.rfile.read(int(self.headers.get('Content-Length','0')))
  if self.path.endswith('/media'):
   calls.append(('MetaMedia',{'bytes':len(raw)}));return self.answer(200,{'id':'uploaded-fixture-id'})
  if self.path.startswith('/meta/'):
   data=json.loads(raw);target=data['to'];calls.append(('Meta',data));providerid='wamid.'+uuid.uuid4().hex
  else:
   data={k:v[0] for k,v in urllib.parse.parse_qs(raw.decode()).items()};target=data['To'];calls.append(('Twilio',data));providerid='SM'+uuid.uuid4().hex
  status=failures.pop(target,200)
  if status!=200:return self.answer(status,{'error':{'code':'test-transient' if status>=429 else 'test-permanent'}})
  return self.answer(200,{'messages':[{'id':providerid}]} if self.path.startswith('/meta/') else {'sid':providerid,'status':'queued','price':None,'price_unit':'USD'})
server=ThreadingHTTPServer(('127.0.0.1',5085),Mock);threading.Thread(target=server.serve_forever,daemon=True).start()
import socketserver
class SMTP(socketserver.StreamRequestHandler):
 def handle(self):
  self.wfile.write(b'220 test SMTP\r\n');self.wfile.flush()
  while True:
   line=self.rfile.readline()
   if not line:return
   command=line.split(b' ')[0].strip().upper()
   if command in (b'EHLO',b'HELO'):reply=b'250-localhost\r\n250 SIZE 10000000\r\n'
   elif command==b'DATA':
    self.wfile.write(b'354 End with dot\r\n');self.wfile.flush();data=[]
    while True:
     row=self.rfile.readline()
     if row in (b'.\r\n',b''):break
     data.append(row)
    receipts.append(b''.join(data));reply=b'250 queued\r\n'
   elif command==b'QUIT':self.wfile.write(b'221 bye\r\n');self.wfile.flush();return
   else:reply=b'250 OK\r\n'
   self.wfile.write(reply);self.wfile.flush()
smtp=socketserver.ThreadingTCPServer(('127.0.0.1',2527),SMTP);threading.Thread(target=smtp.serve_forever,daemon=True).start()

assert DB.startswith('clubs_communication_admin_qa_')
# This suite owns a disposable clone. Existing work and real providers are untouched.
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+DB+';Username=clubs','Logging__LogLevel__Default':'Warning','Communications__MockBase':'http://127.0.0.1:5085','Communications__PollMilliseconds':'100','Smtp__Host':'127.0.0.1','Smtp__Port':'2527','Smtp__EnableSsl':'false','Smtp__From':'noreply@example.test','Smtp__VerifiedSenders__0':'admin-sender@example.test','PublicOrigin':'http://127.0.0.1:5174'}
log=open('.infra/communication-admin-qa.log','w',encoding='utf-8')
assembly=os.environ.get('CLUBS_TEST_ASSEMBLY',str(pathlib.Path('output/communication-build/Clubs.Api.dll').resolve()))
migration=subprocess.run(['dotnet',assembly,'--migrate'],env=env,stdout=log,stderr=log)
assert migration.returncode==0,'Migration failed; see QA log'
sql('''UPDATE "CommunicationAccounts" SET "Enabled"=false;
UPDATE "Mails" SET "DeadLetter"=true WHERE "SentAt" IS NULL;
UPDATE "CommunicationBroadcasts" SET "Status"='Cancelled';
UPDATE "CommunicationAutomations" SET "Status"='Cancelled';
INSERT INTO "Users" ("Id","Email","PasswordHash","Verified","PlatformAdmin","FailedAttempts")
SELECT gen_random_uuid(),'main.admin.qa@example.test',"PasswordHash",true,false,0 FROM "Users" WHERE "Email"='admin@example.test'
ON CONFLICT ("Email") DO NOTHING;
INSERT INTO "UserProfiles"("Id","Name","Phone","Locale","TimeZone","Theme")
SELECT "Id",'QA Main Admin','','en','Asia/Kolkata','light' FROM "Users" WHERE "Email"='main.admin.qa@example.test' ON CONFLICT DO NOTHING;
INSERT INTO "Assignments" ("Id","UserId","TenantId","RoleId","OrgId")
SELECT gen_random_uuid(),u."Id",a."TenantId",a."RoleId",a."OrgId" FROM "Users" u CROSS JOIN "Assignments" a
JOIN "Users" p ON p."Id"=a."UserId" WHERE u."Email"='main.admin.qa@example.test' AND p."Email"='admin@example.test'
AND NOT EXISTS(SELECT 1 FROM "Assignments" old WHERE old."UserId"=u."Id");''')
process=subprocess.Popen(['dotnet',assembly,'--urls',support.BASE],env=env,stdout=log,stderr=log)
try:
 import urllib.request
 for _ in range(100):
  try:urllib.request.urlopen(support.BASE+'/health');break
  except:time.sleep(.2)
 c=Client();assert c.login('admin@example.test')[0]==200
 root=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin');assert c.activate(root)[0] in (200,204)
 reviewer=Client();assert reviewer.login('main.admin.qa@example.test')[0]==200
 rw=next(w for w in reviewer.req('/workspaces')[1] if w['role']=='Association Super Admin');reviewer.activate(rw)
 check('Platform Admin and non-platform Main Admin can open Center',good(c,'/capabilities') is not None and good(reviewer,'/capabilities') is not None)
 check('Main Admin is tested without PlatformAdmin flag',sql('''SELECT "PlatformAdmin" FROM "Users" WHERE "Email"='main.admin.qa@example.test' ''')=='f')
 units=good(c,'/organizations');org=lambda code:next(x['id'] for x in units if x['code']==code)
 check('Organization picker exposes complete hierarchy beyond 100 units',len(units)>100 and {'Club','Zone','District'}<=set(x['level'] for x in units))
 areas=[('all',root['orgId'],3125),('zone',org('DEMO-R01-D01-Z01'),25),('district',org('DEMO-R01-D01'),125),('club',org('DEMO-R01-D01-Z01-C01'),5)]
 people=json.loads(sql('''SELECT json_agg(x) FROM (SELECT p."Id" AS id,p."UserId" AS uid,u."Email" AS email FROM "Persons" p JOIN "Users" u ON u."Id"=p."UserId" WHERE u."Email" IN ('member.club1@example.test','member.club2@example.test','member.zone2@example.test','member.district2@example.test','member.region2@example.test'))x'''))
 first=next(p for p in people if p['email']=='member.club1@example.test')
 # Make only the five fictional recipients reachable on both channels.
 sql('''UPDATE "Users" SET "Verified"=false WHERE "Email" NOT IN ('admin@example.test','main.admin.qa@example.test') AND "Email" NOT LIKE 'member.%@example.test';
UPDATE "CommunicationPreferences" SET "Transactional"=false WHERE "UserId" NOT IN (SELECT "Id" FROM "Users" WHERE "Email" LIKE 'member.%@example.test');
UPDATE "Users" SET "Verified"=true WHERE "Email" LIKE 'member.%@example.test';
UPDATE "PersonPrivacy" SET "TransactionalEmail"=true WHERE "PersonId" IN (SELECT p."Id" FROM "Persons" p JOIN "Users" u ON u."Id"=p."UserId" WHERE u."Email" LIKE 'member.%@example.test');
DELETE FROM "CommunicationSuppressions";''')
 for i,p in enumerate(people):
  p['phone']='+91900000000'+str(i+1)
  sql(f'''DELETE FROM "PersonContacts" WHERE "PersonId"='{p['id']}' AND "Kind"='Phone';
INSERT INTO "PersonContacts"("Id","PersonId","Kind","Label","Value","NormalizedValue","Verified") VALUES(gen_random_uuid(),'{p['id']}','Phone','QA','{p['phone']}','{p['phone']}',true);
INSERT INTO "CommunicationPreferences"("Id","UserId","Channel","Transactional","Promotional","Direct","Birthday","QuietStart","QuietEnd","QuietEnabled","UpdatedAt")
SELECT gen_random_uuid(),'{p['uid']}',ch,true,true,true,false,22,8,false,now() FROM unnest(ARRAY['Email','WhatsApp']) ch
ON CONFLICT ("UserId","Channel") DO UPDATE SET "Transactional"=true,"Promotional"=true,"QuietEnabled"=false;''')
 def preview(client,oid,channel,rules):
  return good(client,'/audiences/preview','POST',{'orgId':oid,'channel':channel,'purpose':'Transactional','rules':json.dumps(rules)})
 for name,oid,count in areas:
  for channel in ['Email','WhatsApp']:
   data=preview(c,oid,channel,{'orgId':oid})
   check(name+' '+channel+' audience count matches independent hierarchy',data['eligible']==count,data)
 data=preview(c,root['orgId'],'Email',{'orgId':root['orgId'],'personIds':[first['id'],first['id']]})
 check('Individual selection deduplicates a person',data['eligible']==1 and data['sample'][0]['id']==first['id'])
 check('Empty individual selection never targets everyone',preview(c,root['orgId'],'Email',{'personIds':[]})['eligible']==0)
 outside=next(p for p in people if p['email']=='member.region2@example.test')
 check('Individual outside selected club is excluded',preview(c,areas[-1][1],'Email',{'personIds':[outside['id']]})['eligible']==0)
 check('Unknown person does not expand recipient set',preview(c,root['orgId'],'Email',{'personIds':[str(uuid.uuid4())]})['eligible']==0)
 found=good(c,'/recipient-search?'+urllib.parse.urlencode({'orgId':areas[-1][1],'search':''}))
 check('Member search remains within selected club',first['id'] in [p['id'] for p in found] and outside['id'] not in [p['id'] for p in found])
 check('Oversized individual list is rejected',c.req('/communications/audiences/preview','POST',{'orgId':root['orgId'],'rules':json.dumps({'personIds':[first['id']]*501})})[0]==400)
 # Real authenticated sessions, including member sessions which use OTP in the product.
 def session_client(email):
  import http.cookiejar
  token=uuid.uuid4().hex;digest=hashlib.sha256(token.encode()).hexdigest().upper()
  sql(f'''INSERT INTO "Sessions"("Id","UserId","CreatedAt","ExpiresAt","WorkspaceId") SELECT '{digest}',u."Id",now(),now()+interval '1 hour',a."Id" FROM "Users" u JOIN "Assignments" a ON a."UserId"=u."Id" WHERE u."Email"='{email}' LIMIT 1''')
  client=Client();client.jar.set_cookie(http.cookiejar.Cookie(0,'clubs_session',token,None,False,'127.0.0.1',False,False,'/',True,False,None,True,None,None,{},False));return client
 for role in ['member.club1','club.president','zonal.president','district.president','regional.president','national.president','international.president']:
  limited=session_client(role+'@example.test')
  paths=['/capabilities','/organizations','/accounts','/campaigns','/templates','/analytics','/member-scope','/conversations']
  codes=[limited.req('/communications'+p)[0] for p in paths]
  check(role+' cannot access Center APIs',all(code==403 for code in codes),codes)
  check(role+' cannot preview or send',limited.req('/communications/audiences/preview','POST',{'orgId':root['orgId']})[0]==403 and limited.req('/communications/campaigns','POST',{'orgId':root['orgId'],'title':'Unauthorized','body':'Blocked'})[0]==403)
 def account(provider,channel,sender):
  b=good(c,'/accounts','POST',{'orgId':root['orgId'],'name':'Admin QA '+provider+' '+uuid.uuid4().hex[:6],'channel':channel,'provider':provider,'sender':sender,'reference':'fixture-phone' if provider=='Meta' else '', 'businessId':'fixture-waba','secret':'fixture-token','webhookSecret':'fixture-app-secret','verifyToken':'fixture-verifier','callbackUrl':'https://callback.example.test/meta','enabled':True,'inbound':provider=='Meta','perMinute':600})
  good(c,'/accounts/'+b['id']+'/verify','POST',{});return b
 wa=account('Meta','WhatsApp','+919999999999');em=account('SMTP','Email','admin-sender@example.test')
 untrusted=good(c,'/accounts','POST',{'orgId':root['orgId'],'name':'Admin QA untrusted','channel':'Email','provider':'SMTP','sender':'unverified@example.test','enabled':True})
 check('Unallowlisted email sender cannot be verified',c.req('/communications/accounts/'+untrusted['id']+'/verify','POST',{})[0]==409)
 check('Unverified email sender cannot create a campaign',c.req('/communications/campaigns','POST',{'orgId':root['orgId'],'title':'Rejected sender','body':'QA','channel':'Email','accountId':untrusted['id']})[0]==409)
 check('WhatsApp broadcast cannot send without an approved template',c.req('/communications/campaigns','POST',{'orgId':root['orgId'],'title':'Rejected WhatsApp','body':'QA','channel':'WhatsApp','accountId':wa['id']})[0]==409)
 good(c,'/accounts/'+wa['id']+'/sync-templates','POST',{})
 template=next(t for t in good(c,'/templates') if t.get('accountId')==wa['id'] and t['providerName']=='community_hello')
 check('Email and WhatsApp senders verify and approved WhatsApp template syncs',all(next(a for a in good(c,'/accounts') if a['id']==v['id'])['verified'] for v in [wa,em]) and template['providerStatus']=='APPROVED')
 check('Main Admin can compose email outreach without PlatformAdmin flag',reviewer.req('/communications/campaigns','POST',{'orgId':root['orgId'],'title':'Main Admin QA draft','body':'Hello {{member_name}}','channel':'Email','accountId':em['id'],'purpose':'Transactional','rules':json.dumps({'personIds':[first['id']]})})[0]==200)
 def approve(b):
  b=good(c,'/campaigns/'+b['id']+'/status','POST',{'action':'Submit','version':b['version']})
  request=c.req('/governance/requests/'+b['approvalRequestId'])[1]['request']
  code,data=reviewer.req('/governance/requests/'+request['id']+'/decisions','POST',{'decision':'Approve','comment':'Isolated communication QA','version':request['version']});assert code==200,data
  return good(c,'/campaigns/'+b['id'])['broadcast']
 def delivered(bid):
  return json.loads(sql(f'''SELECT COALESCE(json_agg(x),'[]') FROM (SELECT m."Recipient" AS recipient,m."DeliveryStatus" AS status,m."ProviderId" AS provider FROM "Mails" m JOIN "CommunicationMessages" x ON x."Id"=m."CommunicationId" WHERE x."BroadcastId"='{bid}' AND m."DeliveryStatus" IN ('Accepted','Delivered'))x'''))
 campaigns=[]
 individual=('individual',root['orgId'],1)
 for channel,account_ in [('Email',em),('WhatsApp',wa)]:
  for name,oid,count in areas+[individual]:
   rules={'orgId':oid,**({'personIds':[first['id']]} if name=='individual' else {})}
   body=template['body'] if channel=='WhatsApp' else 'Hello {{member_name}}, QA '+name+' update.'
   payload={'orgId':root['orgId'],'title':'Admin QA '+channel+' '+name,'body':body,'subject':'Admin QA '+name,'channel':channel,'accountId':account_['id'],'purpose':'Transactional','rules':json.dumps(rules)}
   if channel=='WhatsApp':payload.update(templateId=template['id'],subject=template['subject'])
   b=good(c,'/campaigns','POST',payload)
   check(channel+' '+name+' cannot bypass approval',c.req('/communications/campaigns/'+b['id']+'/status','POST',{'action':'Schedule','version':b['version']})[0]==409)
   b=approve(b)
   expected_names={'all':['club1','club2','zone2','district2','region2'],'zone':['club1','club2'],'district':['club1','club2','zone2'],'club':['club1'],'individual':['club1']}[name]
   expected={p['email'] if channel=='Email' else p['phone'] for p in people if p['email'] in ['member.'+n+'@example.test' for n in expected_names]}
   frozen=sql(f'''SELECT count(*) FROM "CommunicationRecipients" WHERE "BroadcastId"='{b['id']}' ''')
   check(channel+' '+name+' freezes exactly the intended audience',int(frozen)==count,frozen)
   good(c,'/campaigns/'+b['id']+'/status','POST',{'action':'Schedule','version':b['version']})
   until(lambda:sql(f"SELECT \"Status\" FROM \"CommunicationBroadcasts\" WHERE \"Id\"='{b['id']}'") in ['Completed','Partially Completed'],timeout=60)
   rows=until(lambda:(r if len(r)==len(expected) and all(x['status']=='Accepted' for x in r) else None) if (r:=delivered(b['id'])) else None,timeout=60)
   check(channel+' '+name+' transport sends only expected fictional recipients',{r['recipient'] for r in rows}==expected,rows)
   campaigns.append((channel,b,rows))
 campaign_receipts=[raw for raw in receipts if (email_parser.message_from_bytes(raw).get('Subject') or '').startswith('Admin QA ')]
 check('SMTP received personalized email with selected sender',len(campaign_receipts)==sum(len(r) for ch,b,r in campaigns if ch=='Email') and all(email_parser.utils.parseaddr(email_parser.message_from_bytes(raw).get('From'))[1]=='admin-sender@example.test' and '{{member_name}}' not in raw.decode(errors='replace') for raw in campaign_receipts),{'received':len(campaign_receipts),'expected':sum(len(r) for ch,b,r in campaigns if ch=='Email'),'headers':[dict(email_parser.message_from_bytes(raw).items()) for raw in campaign_receipts]})
 check('WhatsApp used approved templates and personalized parameters',len(calls)==sum(len(r) for ch,b,r in campaigns if ch=='WhatsApp') and all(data['type']=='template' and data['template']['name']=='community_hello' and '{{member_name}}' not in json.dumps(data) for kind,data in calls))
 channel,b,rows=next(v for v in campaigns if v[0]=='WhatsApp')
 payload={'entry':[{'changes':[{'value':{'metadata':{'phone_number_id':'fixture-phone'},'statuses':[{'id':rows[0]['provider'],'status':'delivered','timestamp':str(int(time.time()))}]}}]}]}
 raw=json.dumps(payload).encode();signature='sha256='+hmac.new(b'fixture-app-secret',raw,hashlib.sha256).hexdigest()
 check('WhatsApp rejects forged delivery confirmation',c.request('/communications/webhooks/meta/'+wa['id'],'POST',raw,{'Content-Type':'application/json','X-Hub-Signature-256':'invalid'})[0]==403)
 check('Signed WhatsApp delivery confirmation updates persisted status',c.request('/communications/webhooks/meta/'+wa['id'],'POST',raw,{'Content-Type':'application/json','X-Hub-Signature-256':signature})[0]==200 and any(r['status']=='Delivered' for r in delivered(b['id'])))
 from playwright.sync_api import sync_playwright, expect
 frontend_log=open('.infra/communication-admin-browser.log','w',encoding='utf-8')
 frontend=subprocess.Popen(['node',str(pathlib.Path('client/node_modules/vite/bin/vite.js').resolve()),'--host','127.0.0.1','--port','1246','--strictPort'],cwd='client',env={**os.environ,'CLUBS_API_TARGET':support.BASE},stdout=frontend_log,stderr=frontend_log)
 try:
  for _ in range(80):
   try:urllib.request.urlopen('http://127.0.0.1:1246');break
   except:time.sleep(.2)
  pathlib.Path('docs/qa/communication-admin').mkdir(parents=True,exist_ok=True)
  with sync_playwright() as playwright:
   browser=playwright.chromium.launch()
   def context_for(client):
    context=browser.new_context(viewport={'width':1440,'height':1000})
    context.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path,'httpOnly':True,'secure':False,'sameSite':'Lax'} for x in client.jar])
    return context
   context=context_for(c);page=context.new_page();errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
   page.goto('http://127.0.0.1:1246/app/communications/Campaigns')
   expect(page.get_by_role('button',name='Create campaign',exact=True)).to_be_visible(timeout=20000)
   page.get_by_role('button',name='Create campaign',exact=True).click()
   page.get_by_label('Campaign title',exact=True).fill('Browser targeting QA')
   page.get_by_role('button',name='Continue',exact=True).click()
   expect(page.get_by_role('combobox',name='Verified sender',exact=True)).to_be_visible()
   page.get_by_role('combobox',name='Verified sender',exact=True).select_option(em['id'])
   page.get_by_role('button',name='Continue',exact=True).click()
   check('Platform Admin browser defaults to Email campaign',page.get_by_role('heading',name='Audience',exact=True).is_visible())
   for level,code in [('Zone','DEMO-R01-D01-Z01'),('District','DEMO-R01-D01'),('Club','DEMO-R01-D01-Z01-C01')]:
    page.get_by_label('Organization level',exact=True).select_option(level)
    page.get_by_label('Recipient organization',exact=True).select_option(org(code))
    check('Browser can select '+level+' targeting',page.get_by_label('Recipient organization',exact=True).input_value()==org(code))
   page.get_by_role('combobox',name='Recipients',exact=True).select_option('individual')
   boxes=page.locator('fieldset input[type=checkbox]');expect(boxes.first).to_be_visible();boxes.first.check()
   check('Browser can select an individual member',page.get_by_text('Individual members (1 selected)',exact=True).is_visible())
   page.screenshot(path='docs/qa/communication-admin/platform-individual.png',full_page=True)
   page.get_by_role('button',name='Cancel',exact=True).click()
   check('Admin Center renders without browser errors',not errors,errors)
   page.set_viewport_size({'width':390,'height':844});page.reload();expect(page.get_by_role('button',name='Create campaign',exact=True)).to_be_visible()
   check('Communication Center fits mobile viewport',page.evaluate('document.documentElement.scrollWidth <= innerWidth'))
   page.screenshot(path='docs/qa/communication-admin/platform-mobile.png',full_page=True)
   for client,label in [(reviewer,'Main Admin'),(session_client('member.club1@example.test'),'Member'),(session_client('district.president@example.test'),'District President')]:
    ctx=context_for(client);p=ctx.new_page();p.goto('http://127.0.0.1:1246/app/communications/Campaigns')
    if label=='Main Admin':
     expect(p.get_by_role('button',name='Create campaign',exact=True)).to_be_visible(timeout=20000)
    else:
     expect(p.get_by_text('Communication Center is available only to Main Admin and Platform Admin.',exact=True)).to_be_visible(timeout=20000)
     check(label+' has no Communication Center navigation link',p.locator('a[href="/app/communications"]').count()==0)
    check(label+' browser access restriction is correct',True)
    ctx.close()
   browser.close()
 finally:
  frontend.terminate();frontend.wait(timeout=15);frontend_log.close()
 print(str(len(results))+' communication admin checks passed; local SMTP/Meta only.',flush=True)
finally:
 process.terminate();process.wait(timeout=15)
 sql('''UPDATE "CommunicationAccounts" SET "Enabled"=false WHERE "Name" LIKE 'Admin QA %' ''')
 server.shutdown();smtp.shutdown();server.server_close();smtp.server_close();log.close()
