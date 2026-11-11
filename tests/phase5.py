"""Phase 5 PostgreSQL conversation, governance, consent and durable campaign integration."""
from support import Client,sql
import uuid,json,pathlib,time,datetime,concurrent.futures
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase5.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None,status=200):
 code,b=c.req('/communications'+p,m,d);assert code==status,(p,code,b);return b
def login(email,role=None,org=None):
 c=Client();assert c.login(email)[0]==200
 ws=c.req('/workspaces')[1];w=next(w for w in ws if (role is None or w['role']==role) and (org is None or w['orgId']==org));c.activate(w);return c,w
def until(fn,timeout=25):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.25)
 raise AssertionError('Timed out waiting for durable worker state')
club=sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-006'");district=sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-004'");tag=uuid.uuid4().hex[:8]
admin,root=login('admin@example.test','Association Super Admin');root=next(w for w in admin.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
review,_=login('secretary@example.test','Association Super Admin',root['orgId']);member,mw=login('member@example.test','Club Member',club);manager,_=login('manager@example.test','Club Manager',club)
uid=lambda email:sql(f"""SELECT "Id" FROM "Users" WHERE "Email"='{email}'""")
mid=uid('member@example.test');aid=uid('admin@example.test');managerid=uid('manager@example.test')
sql(f"""UPDATE "PersonPrivacy" SET "AllowContactRequests"=true WHERE "PersonId" IN(SELECT "Id" FROM "Persons" WHERE "UserId" IN('{mid}','{managerid}','{aid}'))""")
check('authorized member opens communication center',good(member,'/capabilities')['scope']['tenantId']==root['tenantId'])
check('member cannot configure providers',member.req('/communications/accounts','POST',{})[0] in [400,403])
c=good(member,'/conversations','POST',{'orgId':club,'title':'Private member-to-officer '+tag,'userIds':[managerid]});cid=c['id'];check('member-to-officer conversation persisted',sql(f"""SELECT count(*) FROM "Conversations" WHERE "Id"='{cid}'""")=='1')
check('senior officer cannot browse unrelated private conversation',admin.req('/communications/conversations/'+cid)[0]==404)
check('explicit officer participant can open conversation',good(manager,'/conversations/'+cid)['staff'])
message=good(member,'/conversations/'+cid+'/messages','POST',{'body':'A question about our community gathering.','clientKey':'key-'+tag});check('message persisted with actual sender',message['senderId']==mid)
retry=good(member,'/conversations/'+cid+'/messages','POST',{'body':'A question about our community gathering.','clientKey':'key-'+tag});check('message idempotency suppresses normal retry',retry['id']==message['id'])
check('unread count increments only for recipient',next(r for r in good(manager,'/conversations')['items'] if r['conversation']['id']==cid)['unread']==1)
good(manager,'/conversations/'+cid+'/read','POST',{'muted':True},204);check('read receipt and mute persist',good(manager,'/conversations/'+cid)['muted'] and next(r for r in good(manager,'/conversations')['items'] if r['conversation']['id']==cid)['unread']==0)
check('member cannot post internal note',member.req('/communications/conversations/'+cid+'/messages','POST',{'body':'Hidden','clientKey':'note-'+tag,'internal':True})[0]==403)
note=good(manager,'/conversations/'+cid+'/messages','POST',{'body':'Assigned follow-up for staff only.','clientKey':'staff-'+tag,'internal':True});check('internal note hidden from member history',all(m['id']!=note['id'] for m in good(member,'/conversations/'+cid+'/messages')['items']))
check('member cannot reply to hidden internal note',member.req('/communications/conversations/'+cid+'/messages','POST',{'body':'Reply','clientKey':'reply-'+tag,'replyToId':note['id']})[0]==400)
check('safe rendering rejects scripts',member.req('/communications/conversations/'+cid+'/messages','POST',{'body':'<script>alert(1)</script>','clientKey':'script-'+tag})[0]==400)
c=good(manager,'/conversations/'+cid)['conversation'];c=good(manager,'/conversations/'+cid,'PUT',{'status':'Pending','priority':'High','tags':'membership,followup','assignedId':managerid,'version':c['version']});check('inbox assignment priority and tags persist',c['assignedId']==managerid and c['priority']=='High')
check('conversation optimistic concurrency enforced',manager.req('/communications/conversations/'+cid,'PUT',{'status':'Resolved','priority':'Normal','tags':'','assignedId':None,'version':1})[0]==409)
check('attachment signature validation',member.upload('/communications/messages/'+message['id']+'/attachments','bad.pdf',b'not a PDF')[0]==400)
code,attachment=member.upload('/communications/messages/'+message['id']+'/attachments','brief.pdf',b'%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF',content_type='application/pdf');check('attachment stored as authorized object',code==200 and sql(f"""SELECT octet_length("StorageKey")>0 FROM "CommunicationAttachments" WHERE "Id"='{attachment.get("id")}'""")=='t',attachment)
check('participant downloads attachment',member.request('/communications/attachments/'+attachment['id'])[0]==200);check('unrelated senior denied attachment',admin.request('/communications/attachments/'+attachment['id'])[0]==404)
check('invalid template variables rejected',admin.req('/communications/templates','POST',{'orgId':club,'name':'Invalid','body':'{{private_medical}}'})[0]==400)
t=good(admin,'/templates','POST',{'orgId':club,'name':'Community welcome '+tag,'body':'Hello {{member_name}}, welcome to {{club_name}}.','channel':'InApp'});t=good(admin,'/templates/'+t['id'],'PUT',{'orgId':club,'name':t['name'],'body':'Hello {{member_name}}, welcome to our connected community.','channel':'InApp','version':t['version']});check('template version history preserved',len(good(admin,'/revisions/'+t['id']))==2)
preview=good(admin,'/audiences/preview','POST',{'orgId':club,'rules':json.dumps({'orgId':club,'any':[{'orgId':club},{'orgId':club}]}),'channel':'InApp','purpose':'Transactional'});expected=int(sql(f"""SELECT count(DISTINCT "PersonId") FROM "Memberships" WHERE "ClubId"='{club}' AND "Status"='Active' AND ("EndsAt" IS NULL OR "EndsAt">now())"""));check('nested overlapping audience deduplicates PersonId',preview['eligible']==expected)
check('audience preview does not expose contact methods',all('email' not in p and 'phone' not in p for p in preview['sample']))
check('member cannot originate national broadcasts',member.req('/communications/campaigns','POST',{'orgId':root['orgId'],'title':'Forbidden broadcast','body':'Forbidden'})[0]==403)
def create(kind='Campaign',purpose='Transactional',**kw):return good(admin,'/campaigns','POST',{'orgId':club,'title':'Verified '+kind+' '+tag,'body':'Hello {{member_name}}, this is an approved community notice.','kind':kind,'channel':'InApp','purpose':purpose,'rules':json.dumps({'orgId':club}),**kw})
def action(b,act):return good(admin,'/campaigns/'+b['id']+'/status','POST',{'action':act,'version':b['version']})
def approve(b):
 b=action(b,'Submit');r=admin.req('/governance/requests/'+b['approvalRequestId'])[1]['request'];check('broadcast requester cannot self-approve',admin.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'','version':r['version']})[0]==403);code,resp=review.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent communications review','version':r['version']});assert code==200,resp;return good(admin,'/campaigns/'+b['id'])['broadcast']
b=create();check('unapproved campaign cannot schedule',admin.req('/communications/campaigns/'+b['id']+'/status','POST',{'action':'Schedule','version':b['version']})[0]==409);b=approve(b);check('shared approval engine authorizes campaign',b['status']=='Approved');check('approved campaign cannot materially change',admin.req('/communications/campaigns/'+b['id'],'PUT',{'orgId':club,'title':'Changed','body':'Replacement without authorization','version':b['version']})[0]==409)
recipients=[];cursor=None
while True:
 page=good(admin,'/campaigns/'+b['id']+'/recipients'+('?after='+cursor if cursor else ''));recipients.extend(page['items']);cursor=page['next']
 if not cursor:break
check('approval freezes deduplicated recipient snapshot',len(recipients)==expected and len({r['id'] for r in recipients})==expected)
b=action(b,'Schedule');until(lambda:good(admin,'/campaigns/'+b['id'])['broadcast']['status'] in ['Completed','Partially Completed']);check('durable in-app campaign completed',True)
check('in-app recipient receives personalized persisted message',any('Maya' in n['body'] for n in good(member,'/notifications')))
check('delivery analytics reports actual delivered status',any(x['deliveryStatus']=='Delivered' for x in good(admin,'/analytics')['statuses']))
b=create(kind='Announcement',acknowledgment=True,pinned=True);b=approve(b);action(b,'Schedule');until(lambda:any(a['id']==b['id'] for a in good(member,'/announcements')));good(member,'/announcements/'+b['id']+'/acknowledge','POST',{},204);good(member,'/announcements/'+b['id']+'/acknowledge','POST',{},204);check('announcement acknowledgment idempotent',sql(f"""SELECT count(*) FROM "CommunicationRecipients" WHERE "BroadcastId"='{b["id"]}' AND "UserId"='{mid}' AND "AcknowledgedAt" IS NOT NULL""")=='1')
b=create(sendAt=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(days=1)).isoformat());b=approve(b);b=action(b,'Schedule');b=action(b,'Cancel');check('scheduled campaign cancellation persists',b['status']=='Cancelled' and sql(f"""SELECT count(*) FROM "CommunicationMessages" WHERE "BroadcastId"='{b["id"]}'""")=='0')
good(member,'/preferences','PUT',{'channel':'InApp','transactional':True,'promotional':False});b=create(purpose='Promotional');b=approve(b);action(b,'Schedule');until(lambda:good(admin,'/campaigns/'+b['id'])['broadcast']['status'] in ['Completed','Partially Completed']);check('campaign rechecks promotional consent at delivery',sql(f"""SELECT count(*) FROM "Mails" m JOIN "CommunicationMessages" c ON c."Id"=m."CommunicationId" WHERE c."BroadcastId"='{b["id"]}' AND m."UserId"='{mid}' AND m."Suppressed" """)=='1')
auto=good(admin,'/automations','POST',{'orgId':club,'name':'Approved membership follow-up '+tag,'trigger':'membership.activated','templateId':t['id'],'active':True});check('automation cannot bypass approval with active input',not auto['active'] and auto['status']=='Draft');auto=good(admin,'/automations/'+auto['id']+'/submit','POST',{});r=admin.req('/governance/requests/'+auto['approvalRequestId'])[1]['request'];code,resp=review.req('/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Approved optional follow-up','version':r['version']});check('automation uses independent shared approval',code==200,resp)
other=next(w for w in admin.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['tenantId']!=root['tenantId']);admin.activate(other);check('cross-tenant campaign reports denied',admin.req('/communications/campaigns/'+b['id'])[0]==404);check('cross-tenant private conversation denied',admin.req('/communications/conversations/'+cid)[0]==404)
print(len(results),'Phase 5 checks passed',flush=True)
