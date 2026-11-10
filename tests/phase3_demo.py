"""Create fictional demo records through the real approval APIs; loopback Development only."""
import support
support.BASE='http://127.0.0.1:5080'
from support import Client,sql
import datetime
assert Client().req('/development/mail')[0]==200,'Development-only demonstration'
def good(c,p,m='GET',d=None):
 status,value=c.req(p,m,d);assert status in [200,204],(p,status,value);return value
admin=Client();assert admin.login('admin@example.test')[0]==200;root=next(w for w in good(admin,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
secretary=Client();secretary.login('secretary@example.test');secretary.activate(next(w for w in good(secretary,'/workspaces') if w['role']=='Club Secretary'))
def approve(req):
 submitted=good(admin,'/governance/requests/'+req['id']+'/submit','POST',{'version':req['version']});return good(secretary,'/governance/requests/'+req['id']+'/decisions','POST',{'decision':'Approve','comment':'Reviewed fictional demonstration governance records.','version':submitted['version']})
cfg=good(admin,'/governance/options');club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'','clubs_demo');level=sql('SELECT "LevelId" FROM "Orgs" WHERE "Id"=\''+club+'\'','clubs_demo')
if sql('SELECT count(*) FROM "OfficerAppointments"','clubs_demo')!='0':
 for request in good(admin,'/governance/requests')['items']:
  if request['kind']=='ClubUpdate' and request['status'] in ('Submitted','Under Review'):
   changed=good(secretary,'/governance/requests/'+request['id']+'/decisions','POST',{'decision':'RequestChanges','comment':'Refresh the proposed club operational defaults after migration.','version':request['version']})
   data=good(admin,'/clubs/'+club)['club'];data.update(meetingDay='Saturday',meetingSchedule='First Saturday of each month - 10:00',meetingVenue='Community learning centre',description='A fictional community club connecting local service and leadership.',email='club.office@example.test')
   revised=good(admin,'/governance/requests/'+request['id'],'PUT',{'payload':__import__('json').dumps(data),'version':changed['version']})
   approve(revised)
 if not good(admin,'/governance/resolutions')['items']:
  resolution=good(admin,'/governance/resolutions','POST',{'orgId':club,'number':'RES-2026-01','title':'Annual operating policy review','subject':'Club operating policies','text':'The committee proposes a documented annual review of club administration and leadership continuity. This fictional resolution awaits independent review.','date':'2026-11-11T00:00:00Z','effectiveAt':'2026-12-01T00:00:00Z'})
  good(admin,'/governance/requests/'+resolution['request']['id']+'/submit','POST',{'version':resolution['request']['version']})
 print('Verified/resumed approved fictional governance demonstration.');raise SystemExit()

term=good(admin,'/leadership-terms','POST',{'orgId':club,'name':'Leadership year 2026–27','calendar':'Fiscal','startsAt':'2026-07-01T00:00:00Z','endsAt':'2027-07-01T00:00:00Z','timeZone':'Asia/Kolkata'})
for title,email in [('President','president@example.test'),('Secretary','manager@example.test'),('Membership Director','member@example.test')]:
 position=next(p for p in cfg['positions'] if p['title']==title and p['levelId']==level)
 person=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\''+email+'\')','clubs_demo')
 proposed=good(admin,'/officer-appointments','POST',{'orgId':club,'personId':person,'positionId':position['id'],'termId':term['id'],'startsAt':term['startsAt'],'endsAt':term['endsAt'],'notes':'Fictional demonstration appointment, independently reviewed.'});approve(proposed['request'])
committee=good(admin,'/committees','POST',{'orgId':club,'name':'Community Outreach Committee','code':'OUTREACH-2026','purpose':'Connect members with local service opportunities and coordinate volunteer responsibilities.','category':'Service','description':'A fictional working committee demonstrating scoped administration.','startsAt':'2026-07-01T00:00:00Z','endsAt':'2027-07-01T00:00:00Z'});approve(committee['request'])
chair=next(r for r in cfg['committeeRoles'] if r['name']=='Chairperson');person=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')','clubs_demo')
approve(good(admin,'/committees/'+committee['id']+'/chair','POST',{'personId':person,'roleId':chair['id'],'startsAt':'2026-07-01T00:00:00Z','endsAt':'2027-07-01T00:00:00Z','responsibilities':'Coordinate outreach planning and maintain the committee handover notes.'}))
clubdata=good(admin,'/clubs/'+club)['club'];clubdata.update(meetingDay='Saturday',meetingSchedule='First Saturday of each month · 10:00',meetingVenue='Community learning centre',description='A fictional community club connecting local service and leadership.',email='club.office@example.test');approve(good(admin,'/clubs/'+club,'PUT',clubdata))
resolution=good(admin,'/governance/resolutions','POST',{'orgId':club,'number':'RES-2026-01','title':'Annual operating policy review','subject':'Club operating policies','text':'The committee proposes a documented annual review of club administration and leadership continuity. This fictional resolution is awaiting independent review.','date':'2026-11-11T00:00:00Z','effectiveAt':'2026-12-01T00:00:00Z'});good(admin,'/governance/requests/'+resolution['request']['id']+'/submit','POST',{'version':resolution['request']['version']})
print('Created approved fictional leadership, committee and profile records; one actual resolution awaits review.')
