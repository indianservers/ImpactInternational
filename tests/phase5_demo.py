"""Create fictional communication examples through the real Development API."""
from support import Client,sql
import os,uuid,json
import support
support.BASE=os.environ.get('CLUBS_TEST_URL','http://127.0.0.1:5080')
DB='clubs_demo'
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code==200,(p,code,b);return b
admin=Client();assert admin.login('admin@example.test')[0]==200
assert admin.req('/development/mail')[0]==200,'Demo fixtures require Development API'
root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
club=sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-006'",DB)
uid=lambda email:sql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='{email}'",DB)
memberid=uid('member@example.test');managerid=uid('manager@example.test')
member=Client();member.login('member@example.test');member.activate(next(w for w in good(member,'/workspaces') if w['role']=='Club Member' and w['orgId']==club))
manager=Client();manager.login('manager@example.test');manager.activate(next(w for w in good(manager,'/workspaces') if w['role']=='Club Manager' and w['orgId']==club))
threads=good(admin,'/communications/conversations')['items'];titles={r['conversation']['title'] for r in threads}
samples=[('Community Connect · a warm welcome',[memberid],'Direct',[('admin','Hello Maya! It was wonderful seeing you at Community Connect. Thank you for bringing such thoughtful ideas to the service design session.'),('member','Thank you, Ananya! I really enjoyed meeting everyone. Could you share the next steps for joining the neighborhood reading project?'),('admin','Of course. Our team is meeting on Saturday to plan the reading sessions. I will connect you with the project coordinator. Your experience would be a lovely addition.'),('member','That sounds perfect. I’m happy to help with the first session and bring a few storybooks.')]),('Service planning circle',[memberid,managerid],'Group',[('admin','A small idea, a shared purpose: let’s make our next service day welcoming to every family.'),('manager','I can coordinate the volunteers and venue. Shall we keep the first session to two hours?'),('member','Two hours works well. I can help welcome the children and organize the reading corner.')]),('Leadership in Motion · organizer desk',[managerid],'Support',[('admin','Hi Ravi, are we all set for the Leadership in Motion gathering?'),('manager','The agenda and welcome team are ready. I’m checking the final accessibility arrangements today.'),('admin','Thank you for taking care of the details. Please let me know if the team needs any support.')])]
for title,users,kind,messages in samples:
 if title in titles:continue
 c=good(admin,'/communications/conversations','POST',{'orgId':club,'title':title,'userIds':users,'kind':kind})
 for sender,body in messages:good({'admin':admin,'member':member,'manager':manager}[sender],'/communications/conversations/'+c['id']+'/messages','POST',{'body':body,'clientKey':uuid.uuid4().hex})
 if kind=='Support':
  c=good(admin,'/communications/conversations/'+c['id'])['conversation'];good(admin,'/communications/conversations/'+c['id'],'PUT',{'status':'Pending','priority':'High','tags':'leadership,planning','assignedId':c['ownerId'],'version':c['version']})
existing=good(admin,'/communications/templates')
if not any(t['name']=='A warm community welcome' for t in existing):good(admin,'/communications/templates','POST',{'orgId':club,'name':'A warm community welcome','body':'Hello {{member_name}}, welcome to {{club_name}}. We’re glad you’re part of our community.','channel':'InApp','category':'Welcome'})
if not any(t['name']=='A thoughtful follow-up' for t in existing):good(admin,'/communications/templates','POST',{'orgId':club,'name':'A thoughtful follow-up','body':'Thank you for reaching out. Our team will review your question and get back to you shortly.','channel':'InApp','category':'Member support'})
campaigns=good(admin,'/communications/campaigns');title='Together, a stronger community'
if not any(b['broadcast']['title']==title for b in campaigns):good(admin,'/communications/campaigns','POST',{'orgId':club,'title':title,'body':'Hello {{member_name}}, discover the small ways we can create a lasting impact together.','channel':'InApp','purpose':'Promotional','category':'Member engagement','rules':json.dumps({'orgId':club})})
print('Created idempotent fictional conversations, replies, templates and a draft campaign through real APIs.')
