"""Read-only event communication scope and rejected-write regression checks."""
import uuid
from support import Client
admin=Client();assert admin.login('saisatish@indianservers.com')[0]==200
EVENT='d7476430-45dc-4006-9c7d-230a0fb4210a'
CLUB='18ee9a12-bcc5-464f-bae4-1377038a0162'
OTHER='19a06fac-8eea-477b-af34-db9c8d1ebfe0'
checks=0
def check(ok):
 global checks
 assert ok
 checks+=1
status,units=admin.req('/event-hierarchy');check(status==200);check(any(u['id']==CLUB and u['type']=='Club' for u in units))
path='/events/'+EVENT
status,all_=admin.req(path+'/communication');check(status==200);check(all_['total']>=all_['linked']);check(len({x['id'] for x in all_['items']})==len(all_['items']))
status,own=admin.req(path+'/communication?orgId='+CLUB);check(status==200 and own['total']==all_['total'])
status,outside=admin.req(path+'/communication?orgId='+OTHER);check(status==200 and outside['total']==0)
for segment in ['Confirmed','Awaiting%20RSVP','Not%20registered']:
 status,data=admin.req(path+'/communication?segment='+segment);check(status==200 and data['total']<=all_['total'])
check(admin.req(path+'/communication?segment=Invalid')[0]==400)
check(admin.req(path+'/communication?page=0')[0]==400)
check(admin.req(path+'/member-updates','POST',{'requestId':str(uuid.uuid4()),'message':'x'})[0]==400)
check(admin.req(path+'/member-updates','POST',{'requestId':str(uuid.uuid4()),'message':'QA rejected outside audience','orgId':OTHER})[0]==400)
check(admin.req('/events/03fe8e90-541a-4ec3-8eef-da2846d2739f/member-updates','POST',{'requestId':str(uuid.uuid4()),'message':'QA blocked draft communication'})[0]==409)
member=Client();check(member.login('member.club1@example.test')[0]==200)
check(member.req(path+'/communication')[0]==403)
check(member.req(path+'/member-updates','POST',{'requestId':str(uuid.uuid4()),'message':'QA unauthorized communication'})[0]==403)
club=Client();check(club.login('club.president@example.test')[0]==200)
status,limited=club.req('/event-hierarchy');check(status==200 and all(u['id']!=OTHER for u in limited))
check(club.req('/events/5f37c225-2f2c-455a-a360-47f98fac2efa/communication')[0] in (403,404))
print(f'{checks} event communication checks passed; no messages sent.')
