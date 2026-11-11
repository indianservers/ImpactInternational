"""Idempotent fictional development demo through the real API. No external recipients."""
import support,datetime,uuid,json,urllib.parse
support.BASE='http://127.0.0.1:5080'
from support import Client,sql
c=Client();assert c.req('/development/mail')[0]!=404,'Development API required';assert c.login('admin@example.test')[0]==200
w=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(w)
club=sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-006'",'clubs_demo');person=sql("SELECT \"Id\" FROM \"Persons\" WHERE \"UserId\"=(SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='member@example.test')",'clubs_demo')
def good(path,body):
 code,b=c.req(path,'POST',body);assert code==200,(path,code,b);return b
now=datetime.datetime.now(datetime.timezone.utc);fmt=lambda d:d.isoformat()
existing=c.req('/meetings?'+urllib.parse.urlencode({'from':fmt(now-datetime.timedelta(days=1)),'to':fmt(now+datetime.timedelta(days=30))}))[1]
if not any(m['title']=='Community reading · planning circle' for m in existing):
 m=good('/meetings',{'orgId':club,'title':'Community reading · planning circle','type':'Regular club meeting','startsAt':fmt(now+datetime.timedelta(days=3)),'endsAt':fmt(now+datetime.timedelta(days=3,hours=1)),'location':'Club community room','timeZone':'Asia/Kolkata','requiredPeople':[person],'quorumMinimum':1,'quorumPercent':25})
 code,m=c.req('/meetings/'+m['id']+'/agenda','PUT',{'version':m['version'],'items':[{'id':str(uuid.uuid4()),'title':'Welcome & purpose','duration':10,'description':'A warm welcome and a shared view of the afternoon.'},{'id':str(uuid.uuid4()),'title':'Reading project · next steps','duration':25,'description':'Review volunteer roles, accessibility and the next reading circle.'},{'id':str(uuid.uuid4()),'title':'Decisions & follow-through','duration':15,'description':'Confirm owners and agree the next checkpoint.'}]});assert code==200
 good('/meetings/'+m['id']+'/status',{'status':'Scheduled','version':m['version']})
if not any(p['name']=='Stories that open doors' for p in c.req('/projects')[1]):
 p=good('/projects',{'orgId':club,'name':'Stories that open doors','category':'Education','managerId':person,'description':'A fictional demonstration project bringing neighbors together through reading.','objectives':'Create a welcoming reading space and support accessible learning.','startsAt':fmt(now-datetime.timedelta(days=1)),'endsAt':fmt(now+datetime.timedelta(days=30)),'location':'Community learning room','targetBeneficiaries':30,'plannedVolunteers':8,'plannedHours':24,'estimatedBudget':5000,'currency':'INR'})
 good('/tasks',{'orgId':club,'title':'Welcome our reading volunteers','description':'Prepare an accessible welcome guide and confirm the next planning circle.','personId':person,'sourceType':'Project','sourceId':p['id'],'priority':'Normal','dueAt':fmt(now+datetime.timedelta(days=5))})
print('Fictional meeting, agenda, project draft and task are available through actual application workflows.')
