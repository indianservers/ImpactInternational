"""Read-only assertions for redesigned event projections, filters and aggregates."""
from support import Client,sql
import json,pathlib,datetime,urllib.parse
results=[]
def check(name,ok):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True)
def get(c,path,status=200):
 code,body=c.req(path);check(path+' status '+str(status),code==status);assert code==status,(code,body);return body
admin=Client();assert admin.login('admin@example.test')[0]==200
w=next(w for w in get(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(w)
year=datetime.datetime.now().year;query=f'?from={year}-01-01&to={year+1}-01-01'
d=get(admin,'/events-dashboard'+query)
check('Category event totals reconcile',sum(x['count'] for x in d['byType'])==d['eventsCreated'])
check('Organization event totals reconcile',sum(x['events'] for x in d['byClub'])==d['eventsCreated'])
check('Registration trend confirmed totals reconcile',sum(x['confirmed'] for x in d['registrationTrend'])==d['confirmed'])
check('Invitation funnel begins with unique invitation records',d['funnel']['invitations']==d['invitations'])
check('Invitation cohort responses bounded',d['funnel']['responses']<=d['funnel']['invitations'])
check('Invitation responses form a monotonic funnel',d['funnel']['confirmed']<=d['funnel']['responses'])
check('Invitation cohort confirmed bounded',d['funnel']['confirmed']<=d['confirmed'])
check('Invitation cohort actual attendance bounded',d['funnel']['attended']<=d['funnel']['confirmed'])
check('Previous registration timeline returned','previousRegistrationTrend' in d)
check('Previous period data returned',all(k in d['previous'] for k in ['eventsCreated','confirmed','attended','organizations','attendanceRate']))
check('Recorded country registrations bounded',sum(x['registered'] for x in d['byCountry'])<=d['confirmed'])
check('Participant club totals bounded',sum(x['registered'] for x in d['participantClubs'])<=d['confirmed'])
check('Top event attendance bounded',all(x['attended']<=x['registered'] for x in d['topEvents']))
for t in d['byType'][:3]:
 filtered=get(admin,'/events-dashboard'+query+'&typeId='+t['typeId']);check('Category filter aggregate '+t['name'],filtered['eventsCreated']==t['count'])
for venue in ['Physical','Online','Hybrid']:
 filtered=get(admin,'/events-dashboard'+query+'&venueType='+venue);check('Venue result bounded '+venue,filtered['eventsCreated']<=d['eventsCreated'])
get(admin,'/events-dashboard?from=2026-12-01&to=2026-01-01',400)
get(admin,'/events-dashboard?from=2020-01-01&to=2026-01-01',400)
get(admin,'/events-dashboard'+query+'&orgId=00000000-0000-0000-0000-000000000001',403)
a=get(admin,'/events?size=1');b=get(admin,'/events?size=10');check('Discovery totals independent of pagination',a['summary']==b['summary'])
check('Discovery registrations never expose participant contacts',all('email' not in r and 'responses' not in r for r in b['items']))
if b['items']:
 check('Discovery category and organizer projected',all('category' in r and 'organizer' in r and 'registrationWindow' in r for r in b['items']))
 if a['next']:
  second=get(admin,'/events?size=1&after='+a['next']);check('Discovery cursor does not repeat event',a['items'][0]['id']!=second['items'][0]['id']);check('Cursor summary remains full range',second['summary']==a['summary'])
member=Client();assert member.login('member@example.test')[0]==200
club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');member.activate(next(w for w in get(member,'/workspaces') if w['role']=='Club Member' and w['orgId']==club))
summary=get(member,'/my/registrations?summary=true')
person=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')')
count=int(sql(f'SELECT count(*) FROM "EventRegistrations" WHERE "PersonId"=\'{person}\' AND "Status"=\'Confirmed\''))
check('Personal summary covers all confirmed records',summary['confirmed']==count)
check('Personal summary excludes attendee contacts','email' not in json.dumps(summary).lower())
for status in ['Upcoming','Completed','Cancelled','Waitlisted']:
 records=get(member,'/my/registrations?status='+status);check('Personal status '+status,all((r['status']==('Confirmed' if status in ['Upcoming','Completed'] else status) or (status=='Cancelled' and r['eventLifecycle']=='Cancelled')) for r in records))
get(member,'/events-dashboard'+query,403)
code,bookmark=admin.req('/event-filters','POST',{'name':'QA saved event','filters':json.dumps({'kind':'saved-event','eventId':b['items'][0]['id']})})
check('Save event reuses existing private filters API',code==200)
check('Other user cannot delete saved event',member.req('/event-filters/'+bookmark['id'],'DELETE')[0]==404)
check('Owner can remove saved event',admin.req('/event-filters/'+bookmark['id'],'DELETE')[0]==204)
guest=Client()
for route in ['/events','/events-dashboard','/my/registrations?summary=true','/my/invitations','/my/event-duties']:
 get(guest,route,401)
pathlib.Path('docs/qa/events-api-results.json').write_text(json.dumps(results,indent=2))
print(str(sum(r['passed'] for r in results))+'/'+str(len(results))+' passed')
assert all(r['passed'] for r in results)
