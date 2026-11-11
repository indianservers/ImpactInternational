"""Loopback provider contract tests; no external AI or DNS calls."""
import os,json,pathlib,time,threading,subprocess,urllib.request,uuid
from http.server import ThreadingHTTPServer,BaseHTTPRequestHandler
calls=[];txt='';tool='my_membership';arguments='{}';invalid=False
class Fake(BaseHTTPRequestHandler):
 def log_message(self,*args):pass
 def reply(self,data):
  b=json.dumps(data).encode();self.send_response(200);self.send_header('Content-Type','application/json');self.end_headers();self.wfile.write(b)
 def do_POST(self):
  calls.append(json.loads(self.rfile.read(int(self.headers['Content-Length']))));self.reply({'unexpected':[]} if invalid else {'output':[{'type':'function_call','name':tool,'arguments':arguments}]})
 def do_GET(self):self.reply({'Answer':[{'type':16,'data':'"'+txt+'"'}]})
server=ThreadingHTTPServer(('127.0.0.1',5086),Fake);threading.Thread(target=server.serve_forever,daemon=True).start()
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':f"Host=127.0.0.1;Port=55432;Database={os.environ.get('CLUBS_TEST_DATABASE','clubs_test')};Username=clubs",'Logging__LogLevel__Default':'Warning','Intelligence__OpenAI__ApiKey':'local-test-secret','Intelligence__OpenAI__Model':'local-contract-test','Intelligence__OpenAI__TestEndpoint':'http://127.0.0.1:5086/responses','Domains__TestDnsEndpoint':'http://127.0.0.1:5086/dns','PublicOrigin':'https://primary.example.org'}
log=open('.infra/phase8-provider-api.log','w');process=subprocess.Popen(['dotnet',os.environ.get('CLUBS_INTELLIGENCE_ASSEMBLY','server/bin/Debug/net10.0/Clubs.Api.dll'),'--urls','http://127.0.0.1:5084'],env=env,stdout=log,stderr=log)
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5084'
for _ in range(80):
 try:urllib.request.urlopen('http://127.0.0.1:5084/health',timeout=1);break
 except:time.sleep(.2)
from support import Client,sql
results=[];w=None;old=None

def check(name,ok):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase8-providers.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,name

def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code in (200,204),(p,code,b);return b
try:
 a=Client();assert a.login('admin@example.test')[0]==200;w=next(x for x in good(a,'/workspaces') if x['association'].startswith('Global') and x['role']=='Association Super Admin');a.activate(w)
 old=good(a,'/intelligence/branding')['branding'];b={'displayName':'Fictional Chapters','accent':'#126b56','clubTerm':'Chapter','districtTerm':'Region','remoteAiEnabled':True,'aiPolicy':'Questions may be sent to the configured OpenAI provider for tool selection.','version':old['version'] if old else 1}
 saved=good(a,'/intelligence/branding','PUT',b);check('tenant terminology configured',good(a,'/intelligence/branding/current')['identity']['clubTerm']=='Chapter')
 check('branding optimistic concurrency',a.req('/intelligence/branding','PUT',b)[0]==409)
 check('unsafe asset URL rejected',a.req('/intelligence/branding','PUT',{**b,'version':saved['version'],'logo':'javascript:alert(1)'})[0]==400)
 question={'question':'My membership status','useRemoteModel':True,'acknowledgeProvider':True};answer=good(a,'/intelligence/assistant','POST',question);check('provider selects locally authorized tool',answer['tool']=='my_membership' and answer['readOnly'])
 check('strict non-stored routing request',calls[0]['store']==False and calls[0]['parallel_tool_calls']==False and all(t['strict'] and not t['parameters']['additionalProperties'] for t in calls[0]['tools']))
 check('request contains question but no retrieved facts','facts' not in calls[0] and 'documents' not in calls[0] and calls[0]['input']==question['question'])
 check('per request acknowledgment required',a.req('/intelligence/assistant','POST',{**question,'acknowledgeProvider':False})[0]==409)
 tool='arbitrary_sql';check('unknown provider tool rejected',a.req('/intelligence/assistant','POST',question)[0]==400)
 tool='my_membership';arguments='{"tenantId":"foreign"}';check('provider arguments cannot select foreign tenant',a.req('/intelligence/assistant','POST',question)[0]==400)
 invalid=True;check('malformed provider response fails closed',a.req('/intelligence/assistant','POST',question)[0]==502);invalid=False
 check('primary application hostname protected',a.req('/intelligence/domains','POST',{'host':'primary.example.org'})[0]==400)
 domain=good(a,'/intelligence/domains','POST',{'host':'proof-'+uuid.uuid4().hex[:8]+'.example.org'});id=domain['domain']['id'];proof=good(a,'/intelligence/domains/'+id+'/verify','POST',{});check('incorrect TXT cannot verify ownership',not proof['verified'])
 sql(f'''UPDATE "TenantDomains" SET "LastCheckedAt"=NULL WHERE "Id"='{id}' ''');txt=domain['value'];proof=good(a,'/intelligence/domains/'+id+'/verify','POST',{});check('exact TXT verifies ownership only',proof['verified'] and proof['domain']['status']=='Verified')
 check('verified domain still needs ingress provisioning',a.req('/intelligence/domains/'+id+'/activate','POST',{})[0]==409)
 saved=good(a,'/intelligence/branding','PUT',{**b,'version':saved['version'],'disabledFeatures':['assistant']});check('feature policy enforced at API',a.req('/intelligence/assistant','POST',{'question':'My membership'})[0]==403)
finally:
 if w:
  # Restore only this test's tenant configuration, never alter other tenants.
  if old:
   sql('UPDATE "TenantBrandings" SET '+','.join('"'+k+'"='+"'"+str(v).replace("'","''")+"'" for k,v in [('DisplayName',old['displayName']),('Accent',old['accent']),('ClubTerm',old['clubTerm']),('DistrictTerm',old['districtTerm']),('AiPolicy',old['aiPolicy']),('RemoteAiEnabled',str(old['remoteAiEnabled']).lower()),('DisabledFeatures',old['disabledFeatures'])])+f''' WHERE "TenantId"='{w['tenantId']}' ''')
  else:sql(f'''DELETE FROM "TenantBrandings" WHERE "TenantId"='{w['tenantId']}' ''')
 process.terminate();process.wait(timeout=15);server.shutdown();log.close()
print(len(results),'provider contract checks passed')
