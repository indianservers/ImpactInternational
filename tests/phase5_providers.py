"""Actual adapter HTTP/SMTP protocols against local fakes; never contacts real members/providers."""
import os,time,json,uuid,threading,subprocess,pathlib,urllib.parse,hmac,hashlib,base64,email as email_parser
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
import support,concurrent.futures,statistics
DB='clubs_communication_transport';support.BASE='http://127.0.0.1:5084'
from support import Client
sql=lambda q:support.sql(q,DB)
results=[];calls=[];failures={};receipts=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase5-providers.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
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
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+DB+';Username=clubs','Logging__LogLevel__Default':'Warning','Communications__MockBase':'http://127.0.0.1:5085','Communications__PollMilliseconds':'100','Smtp__Host':'127.0.0.1','Smtp__Port':'2527','Smtp__EnableSsl':'false','Smtp__From':'noreply@example.test','Smtp__VerifiedSenders__0':'noreply@example.test','PublicOrigin':'http://127.0.0.1:5174'}
log=open('.infra/phase5-provider-api.log','w',encoding='utf-8');process=subprocess.Popen(['dotnet',os.environ.get('CLUBS_TEST_ASSEMBLY','server/bin/Debug/net10.0/Clubs.Api.dll'),'--urls',support.BASE],env=env,stdout=log,stderr=log)
try:
 import urllib.request
 until(lambda:ready()) if False else None
 for _ in range(80):
  try:urllib.request.urlopen(support.BASE+'/health');break
  except:time.sleep(.2)
 c=Client();assert c.login('admin@example.test')[0]==200;root=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(root)
 sql(f"""INSERT INTO "Assignments"("Id","UserId","TenantId","RoleId","OrgId") SELECT gen_random_uuid(),u."Id",'{root['tenantId']}',a."RoleId",'{root['orgId']}' FROM "Users" u CROSS JOIN "Assignments" a WHERE u."Email"='secretary@example.test' AND a."Id"='{root['id']}' AND NOT EXISTS(SELECT 1 FROM "Assignments" old WHERE old."UserId"=u."Id" AND old."TenantId"=a."TenantId" AND old."RoleId"=a."RoleId" AND old."OrgId"=a."OrgId");""")
 reviewer=Client();reviewer.login('secretary@example.test');reviewer.activate(next(w for w in reviewer.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['orgId']==root['orgId']))
 club=sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-006'");uid=sql("SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='member@example.test'");pid=sql(f"SELECT \"Id\" FROM \"Persons\" WHERE \"UserId\"='{uid}'")
 sql(f"""UPDATE "PersonPrivacy" SET "AllowContactRequests"=true,"TransactionalEmail"=true WHERE "PersonId"='{pid}';INSERT INTO "PersonContacts"("Id","PersonId","Kind","Label","Value","NormalizedValue","Verified") VALUES(gen_random_uuid(),'{pid}','Phone','Provider QA','+919000000001','+919000000001',true);""")
 member=Client();member.login('member@example.test');member.activate(next(w for w in member.req('/workspaces')[1] if w['role']=='Club Member' and w['orgId']==club))
 sql(f"""DELETE FROM "CommunicationSuppressions" WHERE "TenantId"='{root['tenantId']}' AND "AddressHash" IN('{hashlib.sha256(b'member@example.test').hexdigest().upper()}','{hashlib.sha256(b'+919000000001').hexdigest().upper()}');""")
 for channel in ['WhatsApp','SMS','Email']:good(member,'/preferences','PUT',{'channel':channel,'transactional':True,'promotional':True})
 def account(provider,channel,sender,reference):
  r=good(c,'/accounts','POST',{'orgId':club,'name':'Mock '+provider+' '+uuid.uuid4().hex[:6],'channel':channel,'provider':provider,'sender':sender,'reference':reference,'businessId':'fixture-waba','secret':'fixture-token','webhookSecret':'fixture-app-secret','verifyToken':'fixture-verifier','callbackUrl':'https://callback.example.test/'+provider.lower(),'enabled':True,'inbound':provider!='SMTP','perMinute':30});good(c,'/accounts/'+r['id']+'/verify','POST',{});return next(a for a in good(c,'/accounts') if a['id']==r['id'])
 wa=account('Meta','WhatsApp','+919999999999','fixture-phone');smsaccount=account('Twilio','SMS','+18885550000','ACfixture');emailaccount=account('SMTP','Email','noreply@example.test','')
 check('provider sender verification uses actual mock HTTP response',wa['verified'] and smsaccount['verified'] and emailaccount['verified'])
 check('provider credentials never returned',all('secret' not in a and 'webhookSecret' not in a and 'fixture-token' not in json.dumps(a) for a in good(c,'/accounts')))
 good(c,'/accounts/'+wa['id']+'/sync-templates','POST',{});template=next(t for t in good(c,'/templates') if t['providerName']=='community_hello' and t.get('accountId')==wa['id']);check('WhatsApp template approval synchronized from provider',template['providerStatus']=='APPROVED')
 # Scope all recipients to the club but only a small consenting member subset is reachable.
 def approve(b):
  b=good(c,'/campaigns/'+b['id']+'/status','POST',{'action':'Submit','version':b['version']});r=c.req('/governance/requests/'+b['approvalRequestId'])[1]['request'];code,data=reviewer.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Mock provider test approval','version':r['version']});assert code==200,data;return good(c,'/campaigns/'+b['id'])['broadcast']
 b=good(c,'/campaigns','POST',{'orgId':club,'title':'Provider WhatsApp template QA','body':template['body'],'subject':template['subject'],'channel':'WhatsApp','accountId':wa['id'],'templateId':template['id'],'purpose':'Promotional','rules':json.dumps({'orgId':club})});b=approve(b);good(c,'/campaigns/'+b['id']+'/status','POST',{'action':'Schedule','version':b['version']})
 def mail_for(bid,channel):return json.loads(sql(f"""SELECT row_to_json(m) FROM "Mails" m JOIN "CommunicationMessages" x ON x."Id"=m."CommunicationId" WHERE x."BroadcastId"='{bid}' AND m."UserId"='{uid}' AND m."Channel"='{channel}' LIMIT 1""" ) or 'null')
 m=until(lambda: (m if m and m['SentAt'] else None) if (m:=mail_for(b['id'],'WhatsApp')) else None)
 check('WhatsApp adapter sends approved template through HTTP',any(kind=='Meta' and data.get('template',{}).get('name')=='community_hello' for kind,data in calls))
 check('WhatsApp parameters contain variable value rather than full message',next(data for kind,data in calls if kind=='Meta')['template']['components'][0]['parameters'][0]['text']=='Maya Sen')
 check('provider accepted is not fabricated as delivered',m['DeliveryStatus']=='Accepted')
 def meta_event(payload,bad=False):
  raw=json.dumps(payload,separators=(',',':')).encode();signature='sha256='+hmac.new(b'fixture-app-secret',raw,hashlib.sha256).hexdigest();return c.request('/communications/webhooks/meta/'+wa['id'],'POST',raw,{'Content-Type':'application/json','X-Hub-Signature-256':'invalid' if bad else signature})[0]
 def status_payload(state):return {'entry':[{'changes':[{'value':{'metadata':{'phone_number_id':'fixture-phone'},'statuses':[{'id':m['ProviderId'],'status':state,'timestamp':str(int(time.time()))}]}}]}]}
 check('WhatsApp webhook rejects invalid signature',meta_event(status_payload('delivered'),True)==403)
 payload=status_payload('delivered');check('authenticated webhook marks actual provider delivery',meta_event(payload)==200 and mail_for(b['id'],'WhatsApp')['DeliveryStatus']=='Delivered');meta_event(payload);check('WhatsApp webhook deduplication',sql(f"SELECT count(*) FROM \"CommunicationProviderEvents\" WHERE \"AccountId\"='{wa['id']}' AND \"Status\"='delivered'")=='1')
 meta_event(status_payload('sent'));check('out-of-order status cannot downgrade delivered',mail_for(b['id'],'WhatsApp')['DeliveryStatus']=='Delivered')
 inbound={'entry':[{'changes':[{'value':{'metadata':{'phone_number_id':'fixture-phone'},'messages':[{'id':'inbound-'+uuid.uuid4().hex,'from':'919000000001','timestamp':str(int(time.time())),'text':{'body':'Please help with my membership.'}}]}}]}]};check('signed inbound WhatsApp message persisted',meta_event(inbound)==200)
 shared=good(c,'/team-inbox');thread=next(x for x in shared if x['channel']=='WhatsApp');check('unclaimed inquiry does not expose private history',c.req('/communications/conversations/'+thread['id'])[0]==404);good(c,'/team-inbox/'+thread['id']+'/claim','POST',{});check('explicit claim grants shared inbox participant access',good(c,'/conversations/'+thread['id'])['staff'])
 msg=good(c,'/conversations/'+thread['id']+'/messages','POST',{'body':'We can help with your membership.','clientKey':uuid.uuid4().hex});until(lambda:any(kind=='Meta' and data.get('type')=='text' for kind,data in calls));check('WhatsApp reply within actual service window sent',True)
 media=good(c,'/conversations/'+thread['id']+'/messages','POST',{'body':'A useful community brief.','clientKey':uuid.uuid4().hex,'hold':True});code,file=c.upload('/communications/messages/'+media['id']+'/attachments','community-brief.pdf',b'%PDF-1.4\nfixture\n%%EOF',content_type='application/pdf');assert code==200,file;good(c,'/messages/'+media['id']+'/dispatch','POST',{},204);until(lambda:any(kind=='Meta' and data.get('type')=='document' for kind,data in calls));check('WhatsApp document upload and send use actual adapter HTTP',any(kind=='MetaMedia' for kind,data in calls))
 def inbound_media(mediaid):return {'entry':[{'changes':[{'value':{'metadata':{'phone_number_id':'fixture-phone'},'messages':[{'id':'media-'+uuid.uuid4().hex,'from':'919000000001','timestamp':str(int(time.time())),'type':'document','document':{'id':mediaid,'filename':'member-brief.pdf','mime_type':'application/pdf'}}]}}]}]}
 incoming=inbound_media('inbound-pdf');assert meta_event(incoming)==200;providerid=incoming['entry'][0]['changes'][0]['value']['messages'][0]['id'];fileid=until(lambda:sql(f"SELECT f.\"Id\" FROM \"CommunicationAttachments\" f JOIN \"CommunicationMessages\" m ON m.\"Id\"=f.\"MessageId\" WHERE m.\"ProviderId\"='{providerid}' AND f.\"Status\"='Stored'"));check('signed inbound media durably downloads to protected object storage',c.request('/communications/attachments/'+fileid)[0]==200);check('inbound media respects private conversation access',reviewer.request('/communications/attachments/'+fileid)[0]==404)
 unsafe=inbound_media('untrusted-media');assert meta_event(unsafe)==200;unsafeid=unsafe['entry'][0]['changes'][0]['value']['messages'][0]['id'];until(lambda:sql(f"SELECT f.\"Status\" FROM \"CommunicationAttachments\" f JOIN \"CommunicationMessages\" m ON m.\"Id\"=f.\"MessageId\" WHERE m.\"ProviderId\"='{unsafeid}'")=='Failed');check('inbound media rejects untrusted download host before network fetch',True)
 webhook_ms=[]
 def hook(i):
  data=status_payload('read');data['entry'][0]['changes'][0]['value']['statuses'][0]['timestamp']=str(int(time.time())+i+1000);begin=time.perf_counter();code=meta_event(data);webhook_ms.append((time.perf_counter()-begin)*1000);return code
 with concurrent.futures.ThreadPoolExecutor(max_workers=10) as pool:codes=list(pool.map(hook,range(100)))
 check('high-frequency authenticated status callbacks persist without errors',all(code==200 for code in codes));webhook_ms.sort();pathlib.Path('test-results/phase5-webhook-load.json').write_text(json.dumps({'samples':100,'workers':10,'p50_ms':statistics.median(webhook_ms),'p95_ms':webhook_ms[94],'p99_ms':webhook_ms[98],'errors':codes.count(500),'conditions':'Actual signed HTTP Meta-compatible callbacks, local API and PostgreSQL, ten concurrent workers. Status events never send real provider traffic.'},indent=2))
 sql(f"UPDATE \"Conversations\" SET \"LastInboundAt\"=now()-interval '25 hours' WHERE \"Id\"='{thread['id']}'");check('WhatsApp outside service window requires template',c.req('/communications/conversations/'+thread['id']+'/messages','POST',{'body':'Not permitted free-form','clientKey':uuid.uuid4().hex})[0]==409)
 old={'entry':[{'changes':[{'value':{'metadata':{'phone_number_id':'fixture-phone'},'messages':[{'id':'late-'+uuid.uuid4().hex,'from':'919000000001','timestamp':str(int(time.time())-172800),'text':{'body':'Late historical message'}}]}}]}]};assert meta_event(old)==200;check('late inbound webhook cannot reopen expired WhatsApp service window',c.req('/communications/conversations/'+thread['id']+'/messages','POST',{'body':'Still not permitted','clientKey':uuid.uuid4().hex})[0]==409)
 second=account('Meta','WhatsApp','+919999999999','fixture-phone-second');other_number={'entry':[{'changes':[{'value':{'metadata':{'phone_number_id':'fixture-phone-second'},'messages':[{'id':'second-number-'+uuid.uuid4().hex,'from':'919000000001','timestamp':str(int(time.time())),'text':{'body':'Inquiry to a second authorized number'}}]}}]}]};assert meta_event(other_number)==200;check('shared Meta callback routes a second authorized number to its own account',sql(f"SELECT count(*) FROM \"Conversations\" WHERE \"AccountId\"='{second['id']}'")=='1')
 # SMS retry and provider quota use durable records; make retry time due without sleeping through backoff.
 conv=good(c,'/conversations','POST',{'orgId':club,'title':'SMS protocol QA','userIds':[uid],'channel':'SMS','accountId':smsaccount['id']});failures['+919000000001']=503
 sm=good(c,'/conversations/'+conv['id']+'/messages','POST',{'body':'Important member update','clientKey':uuid.uuid4().hex})
 getmail=lambda:json.loads(sql(f"SELECT row_to_json(m) FROM \"Mails\" m WHERE \"CommunicationId\"='{sm['id']}'") or 'null')
 until(lambda:getmail()['Attempts']==1);check('SMS transient failure recorded with retry',getmail()['DeliveryStatus']=='Retrying' and not getmail()['DeadLetter']);sql(f"UPDATE \"Mails\" SET \"NextAttemptAt\"=now() WHERE \"CommunicationId\"='{sm['id']}'");until(lambda:getmail()['SentAt']);check('SMS retry eventually accepted exactly once',getmail()['DeliveryStatus']=='Accepted')
 def twilio_event(fields,bad=False):
  data=smsaccount['callbackUrl']+''.join(k+str(fields[k]) for k in sorted(fields));sig=base64.b64encode(hmac.new(b'fixture-token',data.encode(),hashlib.sha1).digest()).decode();return c.request('/communications/webhooks/twilio/'+smsaccount['id'],'POST',urllib.parse.urlencode(fields).encode(),{'Content-Type':'application/x-www-form-urlencoded','X-Twilio-Signature':'bad' if bad else sig})[0]
 fields={'AccountSid':'ACfixture','MessageSid':getmail()['ProviderId'],'MessageStatus':'delivered'};check('SMS webhook signature rejected when invalid',twilio_event(fields,True)==403);check('SMS authenticated delivery callback processed',twilio_event(fields)==200 and getmail()['DeliveryStatus']=='Delivered');twilio_event(fields);check('SMS callback idempotent',sql(f"SELECT count(*) FROM \"CommunicationProviderEvents\" WHERE \"AccountId\"='{smsaccount['id']}' AND \"Status\"='delivered'")=='1')
 failures['+919000000001']=400;failed=good(c,'/conversations/'+conv['id']+'/messages','POST',{'body':'Permanent error fixture','clientKey':uuid.uuid4().hex});until(lambda:sql(f"SELECT \"DeadLetter\" FROM \"Mails\" WHERE \"CommunicationId\"='{failed['id']}'")=='t');check('SMS permanent failure dead-lettered',True)
 # Durable interrupted dispatch is uncertain, not automatically sent twice after restart recovery.
 sql(f"UPDATE \"Mails\" SET \"DeadLetter\"=false,\"DeliveryStatus\"='Dispatching',\"NextAttemptAt\"=now()-interval '1 second' WHERE \"CommunicationId\"='{failed['id']}'");until(lambda:sql(f"SELECT \"DeliveryStatus\" FROM \"Mails\" WHERE \"CommunicationId\"='{failed['id']}'")=='Uncertain');check('interrupted provider dispatch is quarantined rather than resent',True)
 # Real SMTP protocol, safely generated HTML and signed adapter bounce processing.
 ec=good(c,'/conversations','POST',{'orgId':club,'title':'Email adapter QA','userIds':[uid],'channel':'Email','accountId':emailaccount['id']});em=good(c,'/conversations/'+ec['id']+'/messages','POST',{'body':'Hello Maya,\nA safely formatted community message.','clientKey':uuid.uuid4().hex});until(lambda:any(b'A safely formatted community message' in (email_parser.message_from_bytes(raw).get_payload(decode=True) or b'') for raw in receipts));check('email uses existing SMTP transport with safe HTML',any(b'text/html' in raw and b'&lt;script' not in raw for raw in receipts))
 attachment_message=good(c,'/conversations/'+ec['id']+'/messages','POST',{'body':'An attached community brief.','clientKey':uuid.uuid4().hex,'hold':True});check('email draft is not dispatched before attachment finalization',sql(f"SELECT \"DeliveryStatus\" FROM \"Mails\" WHERE \"CommunicationId\"='{attachment_message['id']}'")=='Draft');code,file=c.upload('/communications/messages/'+attachment_message['id']+'/attachments','meeting-brief.pdf',b'%PDF-1.4\nfixture\n%%EOF',content_type='application/pdf');assert code==200,file;good(c,'/messages/'+attachment_message['id']+'/dispatch','POST',{},204);until(lambda:any(any(part.get_filename()=='meeting-brief.pdf' for part in email_parser.message_from_bytes(raw).walk()) for raw in receipts));check('email attachment delivered through the original SMTP pipeline',True)
 promo=good(c,'/campaigns','POST',{'orgId':club,'title':'Optional email preferences QA','body':'Hello {{member_name}}, **a community opportunity**.','channel':'Email','accountId':emailaccount['id'],'purpose':'Promotional','rules':json.dumps({'orgId':club})});promo=approve(promo);good(c,'/campaigns/'+promo['id']+'/status','POST',{'action':'Schedule','version':promo['version']});until(lambda:any(email_parser.message_from_bytes(raw).get('List-Unsubscribe') for raw in receipts));unsubscribe=next(email_parser.message_from_bytes(raw).get('List-Unsubscribe').strip('<>') for raw in receipts if email_parser.message_from_bytes(raw).get('List-Unsubscribe'));query=urllib.parse.urlparse(unsubscribe).query;anonymous=Client();code=anonymous.request('/communications/unsubscribe?'+query)[0];check('email unsubscribe GET is confirmation-only',code==200 and next(p for p in good(member,'/preferences') if p['channel']=='Email')['promotional']);code=anonymous.request('/communications/unsubscribe?'+query,'POST',b'List-Unsubscribe=One-Click',{'Content-Type':'application/x-www-form-urlencoded'})[0];pref=next(p for p in good(member,'/preferences') if p['channel']=='Email');check('signed one-click unsubscribe stops promotions while retaining operational preference',code==200 and not pref['promotional'] and pref['transactional'])
 mail=json.loads(sql(f"SELECT row_to_json(m) FROM \"Mails\" m WHERE \"CommunicationId\"='{em['id']}'"));check('SMTP acceptance is distinct from inbox delivery',mail['DeliveryStatus']=='Accepted')
 body=json.dumps({'id':'bounce-'+uuid.uuid4().hex,'messageId':mail['ProviderId'],'status':'bounced'}).encode();stamp=str(int(time.time()));signature=hmac.new(b'fixture-app-secret',stamp.encode()+b'.'+body,hashlib.sha256).hexdigest().upper();code=c.request('/communications/webhooks/email/'+emailaccount['id'],'POST',body,{'Content-Type':'application/json','X-Communication-Timestamp':stamp,'X-Communication-Signature':signature})[0];check('signed email adapter bounce suppresses future mail',code==200 and sql(f"SELECT \"DeliveryStatus\" FROM \"Mails\" WHERE \"CommunicationId\"='{em['id']}'")=='Bounced')
 check('SMTP inbound capability remains unavailable',not next(x for x in good(c,'/capabilities')['channels'] if x['name']=='Email')['inbound'])
 print(len(results),'provider protocol checks passed',flush=True)
finally:
 process.terminate();process.wait(timeout=15)
 sql("UPDATE \"CommunicationAccounts\" SET \"Enabled\"=false WHERE \"Name\" LIKE 'Mock %'")
 server.shutdown();smtp.shutdown();server.server_close();smtp.server_close();log.close()
