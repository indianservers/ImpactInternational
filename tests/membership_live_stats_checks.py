"""Live-source reconciliation; temporary QA fixture edits are always restored."""
import os,json,pathlib,sys
from support import Client,sql,BASE
DB='clubs_impact_dues_upgrade_20261113'
assert BASE=='http://127.0.0.1:5095' and os.environ.get('CLUBS_TEST_DATABASE')==DB
checks=[]
def check(name,ok):
 checks.append({'test':name,'passed':bool(ok)});assert ok,name

def get(c,path):
 status,data=c.req(path);assert status==200,(path,status,data);return data
admin=Client();assert admin.login('saisatish@indianservers.com')[0]==200
club=Client();assert club.login('club.president@example.test')[0]==200
foreign=Client();assert foreign.login('member.club2@example.test')[0]==200
apps=json.loads(sql('SELECT coalesce(json_agg(t),\'[]\') FROM (SELECT "Id","Status","CreatedAt" FROM "MembershipApplications" ORDER BY "Id" LIMIT 2) t',DB));assert len(apps)==2
member=json.loads(sql('''SELECT row_to_json(t) FROM(SELECT m."Id",m."Status",m."EndsAt",m."ClubId" FROM "Memberships" m JOIN "Persons" p ON p."Id"=m."PersonId" JOIN "Users" u ON u."Id"=p."UserId" WHERE u."Email"='member.club1@example.test' AND m."Status"='Active' LIMIT 1)t''',DB));mid=member['Id'];cid=member['ClubId']
def analytics(c=admin,query=''):
 return get(c,'/membership/analytics?refresh=1&'+query)
def due(d,n=30):return next(x['count'] for x in d['renewals']['windows'] if x['days']==n)
baseline=analytics();metrics=get(admin,'/membership/metrics')
check('Current total reconciles with member metrics',baseline['summary']['total']==metrics['totalMembers'])
check('Current active count reconciles with member metrics',baseline['summary']['active']==metrics['activeMembers'])
check('Current renewal due reconciles with member metrics',due(baseline)==metrics['upcomingRenewals'])
check('Current pending applications reconcile',baseline['summary']['pendingApplications']==metrics['pendingApplications'])
try:
 sql(f'''UPDATE "Memberships" SET "EndsAt"=now()+interval '30 days'-interval '1 minute' WHERE "Id"='{mid}';''',DB)
 d=analytics();m=get(admin,'/membership/metrics')
 check('Rolling 30-day window includes end of thirtieth day',due(d)==due(baseline)+1)
 check('Rolling renewal count matches Members API',due(d)==m['upcomingRenewals'])
 check('Renewal status drill-down matches KPI',get(admin,'/members?statuses=Renewal%20Due')['total']==m['upcomingRenewals'])
 check('Selected club renewal window narrows correctly',due(analytics(query='clubId='+cid))==1)
 historical=analytics(query='period=custom&from=2026-01-01&to=2026-10-31&clubId='+cid)
 sql(f'''UPDATE "Memberships" SET "Status"='Inactive' WHERE "Id"='{mid}';''',DB)
 d=analytics();m=get(admin,'/membership/metrics')
 check('Live status changes do not depend on history backfill',d['summary']['active']==m['activeMembers']==baseline['summary']['active']-1)
 check('Latest chart point matches live active snapshot',d['trend'][-1]['active']==d['summary']['active'])
 check('Inactive member no longer counted as renewal due',due(d)==due(baseline))
 h=analytics(query='period=custom&from=2026-01-01&to=2026-10-31&clubId='+cid)
 check('Historical active counts remain based on history',historical['summary']['active']==h['summary']['active'])
 for a,status in zip(apps,['Submitted','Changes Requested']):
  sql(f'''UPDATE "MembershipApplications" SET "Status"='{status}',"CreatedAt"='2020-01-01T00:00:00Z' WHERE "Id"='{a['Id']}';''',DB)
 d=analytics(query='period=7d');m=get(admin,'/membership/metrics');pending=get(admin,'/membership/applications?status=Pending')
 check('Old pending applications remain in current backlog',d['summary']['pendingApplications']==m['pendingApplications']==pending['total']==2)
 check('Changing chart period does not hide pending backlog',analytics(query='period=previous-year')['summary']['pendingApplications']==2)
 dashboard=get(admin,'/dashboard?view=platform');p=next(p for p in dashboard['priorities'] if p['id']=='applications')
 check('Platform dashboard uses same application definition',p['count']==2 and 'status=Pending' in p['href'])
 gov=get(admin,'/administration/dashboard');check('Governance dashboard uses same application definition',gov['pendingApplications']==2)
 sql(f'''UPDATE "MembershipApplications" SET "Status"='Cancelled' WHERE "Id"='{apps[1]['Id']}';''',DB)
 check('Cancelled applications are excluded',analytics()['summary']['pendingApplications']==get(admin,'/membership/applications?status=Pending')['total']==1)
 cp=analytics(club);check('Club role backlog matches its authorized pending queue',cp['summary']['pendingApplications']==get(club,'/membership/applications?status=Pending')['total'])
 check('Ordinary members cannot see administrative statistics',foreign.req('/membership/analytics')[0]==403)
finally:
 end='NULL' if member['EndsAt'] is None else "'"+member['EndsAt']+"'"
 sql(f'''UPDATE "Memberships" SET "Status"='{member['Status']}',"EndsAt"={end} WHERE "Id"='{mid}';''',DB)
 for a in apps:sql(f'''UPDATE "MembershipApplications" SET "Status"='{a['Status']}',"CreatedAt"='{a['CreatedAt']}' WHERE "Id"='{a['Id']}';''',DB)
 analytics();analytics(club)
pathlib.Path('docs/qa/membership-live-stats/api-checks.json').write_text(json.dumps(checks,indent=2))
print(str(len(checks))+' live statistics checks passed; temporary changes restored.')
