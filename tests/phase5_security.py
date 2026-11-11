"""Real-time access, abuse boundaries, privacy revocation, retention and automation idempotency."""
from support import Client,sql,BASE
import websocket,uuid,json,pathlib,time,datetime,concurrent.futures,urllib.parse
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase5-security.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,b=c.req('/communications'+p,m,d);assert code==status,(p,code,b);return b
def until(fn,timeout=20):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.2)
 raise AssertionError('Timed out')
def login(email,role,org=None):
 c=Client();assert c.login(email)[0]==200;ws=c.req('/workspaces')[1];w=next(w for w in ws if w['role']==role and (not org or w['orgId']==org));c.activate(w);return c,w
club=sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-006'");tag=uuid.uuid4().hex[:8]
admin,root=login('admin@example.test','Association Super Admin');root=next(w for w in admin.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
review,_=login('secretary@example.test','Association Super Admin',root['orgId']);member,mw=login('member@example.test','Club Member',club);manager,cw=login('manager@example.test','Club Manager',club)
uid=lambda e:sql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='{e}'")
mid=uid('member@example.test');managerid=uid('manager@example.test');aid=uid('admin@example.test')
sql(f"UPDATE \"PersonPrivacy\" SET \"AllowContactRequests\"=true WHERE \"PersonId\" IN(SELECT \"Id\" FROM \"Persons\" WHERE \"UserId\" IN('{mid}','{managerid}','{aid}'))")
good(member,'/preferences','PUT',{'channel':'InApp','transactional':True,'promotional':True,'birthday':True,'direct':True})
c=good(admin,'/conversations','POST',{'orgId':club,'title':'Real-time privacy '+tag,'userIds':[mid,managerid],'kind':'Group'});cid=c['id']
def cookie(c):return '; '.join(x.name+'='+x.value for x in c.jar)
def connect(c,cid,origin=BASE):return websocket.create_connection(BASE.replace('http','ws',1)+'/api/v1/communications/stream?conversationId='+cid,cookie=cookie(c),origin=origin,timeout=5)
ws=connect(member,cid);initial=json.loads(ws.recv());check('WebSocket connection authenticates scoped participant',initial['type']=='invalidate')
m=good(admin,'/conversations/'+cid+'/messages','POST',{'body':'A real-time update from another user.','clientKey':uuid.uuid4().hex});update=json.loads(ws.recv());check('persisted message produces scoped live invalidation',update['marker']!=initial['marker'])
check('WebSocket event contains no private message body','body' not in update and 'A real-time' not in json.dumps(update))
other=next(w for w in admin.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['tenantId']!=root['tenantId']);admin.activate(other);check('same user cannot read tenant-one conversation from tenant-two workspace',admin.req('/communications/conversations/'+cid)[0]==404);admin.activate(root)
try:connect(member,cid,'https://untrusted.example.test');denied=False
except websocket.WebSocketBadStatusException as e:denied=e.status_code==403
check('cross-origin WebSocket rejected',denied)
sql(f"UPDATE \"ConversationParticipants\" SET \"Active\"=false WHERE \"ConversationId\"='{cid}' AND \"UserId\"='{mid}'")
closed=ws.recv();check('revocation closes existing protected live subscription',closed=='');ws.close();check('revoked participant loses message history immediately',member.req('/communications/conversations/'+cid+'/messages')[0]==404)
sql(f"UPDATE \"ConversationParticipants\" SET \"Active\"=true WHERE \"ConversationId\"='{cid}' AND \"UserId\"='{mid}'")
check('staff membership grant cannot be delegated to ineligible user',admin.req('/communications/conversations/'+cid+'/participants','POST',{'userId':mid})[0]==403)
check('message cursor must belong to the same conversation',member.req('/communications/conversations/'+cid+'/messages?before='+str(uuid.uuid4()))[0]==400)
check('null message input rejected rather than server failure',member.req('/communications/conversations/'+cid+'/messages','POST',{'body':None,'clientKey':uuid.uuid4().hex})[0]==400)
check('deep audience expression bounded',admin.req('/communications/audiences/preview','POST',{'orgId':club,'rules':json.dumps({'all':[{'all':[{'all':[{'all':[{'all':[{}]}]}]}]}]})})[0]==400)
check('club sender cannot resolve ancestor audience',manager.req('/communications/audiences/preview','POST',{'orgId':club,'rules':json.dumps({'orgId':root['orgId']})})[0]==404)
check('originating club bounds audience even for headquarters sender',admin.req('/communications/audiences/preview','POST',{'orgId':club,'rules':json.dumps({'orgId':root['orgId']})})[0]==404)
# Effective assignment revocation is checked independently from a user's organizational rank.
sql(f"UPDATE \"ConversationParticipants\" SET \"Active\"=false WHERE \"ConversationId\"='{cid}' AND \"UserId\"='{managerid}'")
check('inactive staff participant cannot access inbox context',manager.req('/communications/conversations/'+cid+'/context')[0]==404)
sql(f"UPDATE \"ConversationParticipants\" SET \"Active\"=true WHERE \"ConversationId\"='{cid}' AND \"UserId\"='{managerid}'")
# Native database guard preserves immutable revision history.
t=good(admin,'/templates','POST',{'orgId':club,'name':'Lifecycle follow-up '+tag,'body':'Hello {{member_name}}, a useful membership update.','channel':'InApp'})
try:sql(f"UPDATE \"CommunicationRevisions\" SET \"Snapshot\"='changed' WHERE \"ResourceId\"='{t['id']}'");immutable=False
except Exception:immutable=True
check('database rejects revision-history mutation',immutable)
rule=good(admin,'/automations','POST',{'orgId':club,'name':'Membership signal '+tag,'trigger':'membership.activated','templateId':t['id'],'delayMinutes':0,'dailyLimit':2});rule=good(admin,'/automations/'+rule['id']+'/submit','POST',{});r=admin.req('/governance/requests/'+rule['approvalRequestId'])[1]['request'];code,body=review.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Approved fixture rule','version':r['version']});assert code==200,body
source=str(uuid.uuid4());sql(f"""INSERT INTO "Mails"("Id","TenantId","OrgId","UserId","Kind","TokenHash","Link","ExpiresAt","Suppressed","Used","Attempts","NextAttemptAt","Recipient","Subject","Body","Channel","Purpose","DeliveryStatus","ProviderId","TemplateName","TemplateParameters","Language","LastError","Currency","DeadLetter","TemplateVersion","CreatedAt") VALUES('{source}','{root['tenantId']}','{club}','{mid}','membership.activated','{uuid.uuid4().hex*2}','',now()+interval '30 days',true,false,0,now(),'','','membership signal','Email','Transactional','Suppressed','','','[]','en','','',false,0,now());""")
until(lambda:sql(f"SELECT count(*) FROM \"CommunicationExecutions\" WHERE \"AutomationId\"='{rule['id']}' AND \"SourceKey\"='{source}'")=='1');check('domain notification signal triggers approved automation',True)
time.sleep(2);check('automation execution idempotency prevents repeated reminder',sql(f"SELECT count(*) FROM \"CommunicationExecutions\" WHERE \"AutomationId\"='{rule['id']}' AND \"SourceKey\"='{source}'")=='1')
# Privacy/quiet hours are applied at delivery, and policy-based history retention preserves record identity.
message=good(member,'/conversations/'+cid+'/messages','POST',{'body':'Retention-private fixture','clientKey':uuid.uuid4().hex});sql(f"UPDATE \"CommunicationMessages\" SET \"At\"=now()-interval '400 days' WHERE \"Id\"='{message['id']}'");until(lambda:sql(f"SELECT \"Body\" FROM \"CommunicationMessages\" WHERE \"Id\"='{message['id']}'")=='[Expired by retention]');check('old private message content redacted without deleting provenance',sql(f"SELECT \"SenderId\"='{mid}' FROM \"CommunicationMessages\" WHERE \"Id\"='{message['id']}'")=='t')
check('legacy outbox rows migrated to email channel',sql('SELECT count(*) FROM "Mails" WHERE "Channel"=\'\'')=='0')
check('unconfigured AI cannot transmit private conversations',good(admin,'/capabilities')['ai']['enabled']==False)
check('SMTP cannot be advertised as an inbound mailbox',all(not x['inbound'] for x in good(admin,'/capabilities')['channels'] if x['name']=='Email'))
check('period analytics exposes unsupported opens honestly',good(admin,'/analytics')['opens']=='Not Available')
print(len(results),'security and real-time checks passed',flush=True)
