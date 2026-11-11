"""Signed Stripe protocol tests against local HTTP fake. No external payment calls."""
import os,json,pathlib,subprocess,time,threading,urllib.request,urllib.parse,hmac,hashlib,uuid
from http.server import ThreadingHTTPServer,BaseHTTPRequestHandler
f=json.loads(pathlib.Path('test-results/phase7-fixture.json').read_text());entity=f['entity']['id'];sessions={};calls=[]
class FakeStripe(BaseHTTPRequestHandler):
 def log_message(self,*args):pass
 def do_POST(self):
  assert self.headers['Authorization']=='Bearer sk_test_phase7_local';data=urllib.parse.parse_qs(self.rfile.read(int(self.headers.get('Content-Length',0))).decode());calls.append({'path':self.path,'key':self.headers['Idempotency-Key']});sid='cs_test_'+uuid.uuid4().hex;sessions[sid]={'id':sid,'url':'https://checkout.stripe.com/c/pay/'+sid,'currency':data['line_items[0][price_data][currency]'][0],'amount_total':int(data['line_items[0][price_data][unit_amount]'][0]),'client_reference_id':data['client_reference_id'][0],'payment_status':'unpaid','payment_intent':'pi_'+uuid.uuid4().hex};self.reply(sessions[sid])
 def do_GET(self):self.reply(sessions[self.path.rsplit('/',1)[1]])
 def reply(self,data):b=json.dumps(data).encode();self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(b)));self.end_headers();self.wfile.write(b)
ThreadingHTTPServer.request_queue_size=128
server=ThreadingHTTPServer(('127.0.0.1',5086),FakeStripe);threading.Thread(target=server.serve_forever,daemon=True).start()
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database=clubs_test;Username=clubs','Logging__LogLevel__Default':'Warning','Finance__Gateways__qa__EntityId':entity,'Finance__Gateways__qa__Provider':'Stripe','Finance__Gateways__qa__SecretKey':'sk_test_phase7_local','Finance__Gateways__qa__WebhookSecret':'whsec_phase7_local','Finance__Gateways__qa__ApiBase':'http://127.0.0.1:5086/v1/','Finance__Gateways__qa__ReturnUrl':'https://example.test/app/finance/member'}
log=open('.infra/phase7-provider-api.log','w');process=subprocess.Popen(['dotnet',os.environ.get('CLUBS_FINANCE_API_ASSEMBLY','server/bin/Debug/net10.0/Clubs.Api.dll'),'--urls','http://127.0.0.1:5084'],env=env,stdout=log,stderr=log)
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5084'
for _ in range(80):
 try:urllib.request.urlopen('http://127.0.0.1:5084/health',timeout=1);break
 except:time.sleep(.2)
from support import Client,sql
results=[]
def check(name,ok,data=None):results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase7-providers.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None):status,b=c.req(p,m,d);assert status in (200,204),(p,status,b);return b
try:
 admin=Client();assert admin.login('admin@example.test')[0]==200;w=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(w);good(admin,'/finance/entities/'+entity+'/gateway','PUT',{'reason':'qa'})
 member=Client();assert member.login('member@example.test')[0]==200;mw=next(w for w in good(member,'/workspaces') if w['role']=='Club Member' and w['orgId']==f['clubId']);member.activate(mw)
 day=time.strftime('%Y-%m-%d');invoice=good(admin,'/finance/entities/'+entity+'/invoices','POST',{'personId':f['personId'],'customer':'Fictional checkout member','purpose':'Other','date':day,'dueOn':day,'key':uuid.uuid4().hex,'lines':[{'description':'Provider protocol QA','quantity':1,'rate':'12.34'}]});good(admin,'/finance/invoices/'+invoice['id']+'/issue','POST',{})
 ikey=uuid.uuid4().hex;intent=good(member,'/finance/invoices/'+invoice['id']+'/payment-intents','POST',{'key':ikey});check('server creates real provider protocol checkout',intent['checkoutUrl'].startswith('https://checkout.stripe.com/') and len(calls)==1 and bool(calls[0]['key']))
 check('provider idempotency intent reused',good(member,'/finance/invoices/'+invoice['id']+'/payment-intents','POST',{'key':ikey})['id']==intent['id'] and len(calls)==1)
 check('refresh resumes active checkout without creating duplicate order',good(member,'/finance/invoices/'+invoice['id']+'/payment-intents','POST',{'key':uuid.uuid4().hex})['id']==intent['id'] and len(calls)==1)
 check('browser redirect never marks invoice paid',good(member,'/finance/invoices/'+invoice['id'])['invoice']['paid']==0)
 session=next(iter(sessions));payload={'id':'evt_'+uuid.uuid4().hex,'type':'checkout.session.completed','data':{'object':{'id':session}}}
 def send(value,secret='whsec_phase7_local',timestamp=None):
  body=json.dumps(value,separators=(',',':')).encode();ts=int(time.time()) if timestamp is None else timestamp;sig=hmac.new(secret.encode(),str(ts).encode()+b'.'+body,hashlib.sha256).hexdigest();return member.request('/finance/webhooks/'+entity,'POST',body,{'Content-Type':'application/json','Stripe-Signature':f't={ts},v1={sig}'})[0]
 check('invalid webhook signature rejected',send(payload,'bad')==400);check('stale signed webhook rejected',send(payload,timestamp=int(time.time())-601)==400);check('unpaid provider session cannot capture',send(payload)==409)
 sessions[session]['payment_status']='paid';sessions[session]['amount_total']=1235;check('provider amount manipulation rejected',send(payload)==409);sessions[session]['amount_total']=1234
 check('verified signature and provider confirmation capture',send(payload)==200);check('invoice allocation exactly equals verified amount',good(member,'/finance/invoices/'+invoice['id'])['invoice']['paid']==12.34);check('duplicate webhook harmless',send(payload)==200)
 check('one payment and one collection journal',int(sql(f'SELECT count(*) FROM "FinancePayments" WHERE "IdempotencyKey"=\'intent:{intent["id"]}\''))==1)
 check('out of order failure cannot downgrade paid',send({'id':'evt_'+uuid.uuid4().hex,'type':'checkout.session.async_payment_failed','data':{'object':{'id':session}}})==200 and good(member,'/finance/invoices/'+invoice['id'])['invoice']['paid']==12.34)
 check('same event ID different body rejected',send({**payload,'type':'checkout.session.expired'})==409)
 check('captured gateway funds remain clearing',sql(f"SELECT \"SettlementStatus\" FROM \"FinancePayments\" WHERE \"IdempotencyKey\"='intent:{intent['id']}'")=='Unsettled')
 check('public callback contains no customer data',member.request('/finance/webhooks/'+entity,'POST',json.dumps(payload).encode(),{'Content-Type':'application/json','Stripe-Signature':'bad'})[0]==400)
 import concurrent.futures,statistics
 def repeat(_):
  begun=time.perf_counter();code=send(payload);return code,(time.perf_counter()-begun)*1000
 with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:replays=list(pool.map(repeat,range(80)))
 check('concurrent signed webhook replay remains idempotent',all(code==200 for code,_ in replays) and int(sql(f'SELECT count(*) FROM "FinancePayments" WHERE "IdempotencyKey"=\'intent:{intent["id"]}\''))==1,{'statuses':[c for c,_ in replays]})
 timings=sorted(t for _,t in replays);pathlib.Path('test-results/phase7-webhook-load.json').write_text(json.dumps({'events':80,'concurrency':8,'p50_ms':round(statistics.median(timings),2),'p95_ms':round(timings[75],2),'errors':sum(code!=200 for code,_ in replays),'conditions':'Local signed Stripe snapshot replay, real adapter HTTP retrieval against loopback fake, shared Windows/PostgreSQL. Repeated event delivery, not 80 distinct payments.'},indent=2))
 check('gateway books still balance',good(admin,'/finance/entities/'+entity+'/reports/trial-balance')['trialDifference']==0)
 print(len(results),'provider checks passed')
finally:
 process.terminate();process.wait(timeout=20);log.close();server.shutdown();server.server_close()
