"""Create approved fictional event demonstrations through the actual API."""
from support import Client
import datetime,time

def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code==200,(p,code,str(b)[:300]);return b
admin=Client();assert admin.req('/development/mail')[0]==200,'Demonstration seeding requires a Development API';admin.login('admin@example.test');root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
review=Client();review.login('president@example.test');review.activate(next(w for w in good(review,'/workspaces') if w['role']=='Club President'))
member=Client();member.login('member@example.test');club=next(w['orgId'] for w in good(member,'/workspaces') if w['role']=='Club Member');member.activate(next(w for w in good(member,'/workspaces') if w['role']=='Club Member'));types=good(admin,'/event-types');now=datetime.datetime.now(datetime.timezone.utc)
existing=good(admin,'/events?size=100')['items'];names={e['title'] for e in existing}
for name,offset,hours,type_name,capacity,description,venue in [
 ('Community Connect: a fellowship evening',-.5,4,'Fellowship Meeting',40,'A fictional demonstration gathering for members and guests. Share stories, meet the people behind our community projects and make new connections.','Community Learning Centre, Vijayawada'),
 ('Leadership in Motion',24*12,5,'Leadership Program',120,'A practical workshop on leading with purpose. Explore team collaboration, meaningful service and a thoughtful transition between leadership terms. This is a fictional demonstration event.','Riverfront Convention Studio, Vijayawada'),
 ('Ideas into Impact: service design lab',24*20,6,'Workshop',80,'A hands-on day of ideas, collaboration and local service planning. Bring a challenge your community cares about. Fictional demonstration only.','Community Innovation Hub, Vijayawada')]:
 if name in names:continue
 start=now+datetime.timedelta(hours=offset);end=start+datetime.timedelta(hours=hours);typeid=next(t['id'] for t in types if t['name']==type_name)
 e=good(admin,'/events','POST',{'orgId':club,'typeId':typeid,'title':name,'description':description,'startsAt':start.isoformat(),'endsAt':end.isoformat(),'registrationStartsAt':(now-datetime.timedelta(days=1)).isoformat(),'registrationEndsAt':(end-datetime.timedelta(minutes=20)).isoformat(),'rsvpDeadline':end.isoformat(),'timeZone':'Asia/Kolkata','venue':venue,'contact':'club.office@example.test','capacity':capacity,'allowGuests':True,'maxCompanions':2,'visibility':'Public','waitlist':True})
 good(admin,'/events/'+e['id']+'/sessions','POST',{'title':'Welcome & shared purpose','speaker':'Community facilitation team','room':'Main studio','startsAt':start.isoformat(),'endsAt':(start+datetime.timedelta(hours=1)).isoformat(),'capacity':capacity})
 good(admin,'/events/'+e['id']+'/sessions','POST',{'title':'Connections into action','speaker':'Local leadership team','room':'Main studio','startsAt':(start+datetime.timedelta(hours=1.5)).isoformat(),'endsAt':(start+datetime.timedelta(hours=2.5)).isoformat(),'capacity':min(60,capacity)})
 e=good(admin,'/events/'+e['id']+'/status','POST',{'action':'Submit','version':e['version']});r=good(admin,'/governance/requests/'+e['approvalRequestId'])['request'];good(review,'/governance/requests/'+r['id']+'/decisions','POST',{'decision':'Approve','comment':'Fictional demonstration event independently reviewed','version':r['version']});e=good(admin,'/events/'+e['id'])['eventInfo'];e=good(admin,'/events/'+e['id']+'/status','POST',{'action':'Publish','version':e['version']})
 good(admin,'/events/'+e['id']+'/invitations','POST',{'message':'Join us for a fictional demonstration of community events and connection.'})
 good(member,'/events/'+e['id']+'/rsvps','POST',{'response':'Going'});reg=good(member,'/events/'+e['id']+'/registrations','POST',{'guestNames':[]})
 if offset<0:good(admin,'/events/'+e['id']+'/checkins','POST',{'registrationId':reg['id'],'device':'Demonstration welcome desk','entryPoint':'Community entrance'})
 good(admin,'/events/'+e['id']+'/feedback-form','PUT',{'questions':'[{"key":"overall","label":"How was your experience?","type":"Rating","required":true},{"key":"thoughts","label":"What would you like us to know?","type":"Long text"}]','anonymous':True,'deadline':(end+datetime.timedelta(days=7)).isoformat()})
 print('Created approved fictional event:',name)
print('Event demonstration verified.')
