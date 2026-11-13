"""Region directory filtering, detail hierarchy, export, and privacy on isolated QA."""
import json, os
from support import Client, sql
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_comms_test_')
a=Client(); assert a.login('admin@example.test')[0]==200
status, regions=a.req('/directory/regions'); assert status==200 and len(regions)>=5
for region in regions:
 status, members=a.req('/members?regionId='+region['id'])
 assert status==200 and members['total']>0
 assert all(m['region']==region['name'] and m['district'] and m['zone'] for m in members['items'])
 status, options=a.req('/membership/options?regionId='+region['id'])
 assert status==200 and all(region['name'] in c['name'] for c in options['clubs'])
assert a.req('/members?regionId=00000000-0000-0000-0000-000000000000')[0]==404
z=Client(); assert z.login('zonal.president@example.test')[0]==200
status, own=z.req('/directory/regions'); assert status==200 and len(own)==1
other=next(r for r in regions if r['id']!=own[0]['id'])
for route in ['/members','/directory','/membership/options']:
 assert z.req(route+'?regionId='+other['id'])[0]==404
status, members=z.req('/members?regionId='+own[0]['id']); assert status==200
assert all(m['region']==own[0]['name'] for m in members['items'])
member=members['items'][0]; person=member['personId']
prior=sql(f'''SELECT "DirectoryVisible"::text||','||"DisplayEmail"::text||','||"DisplayPhone"::text FROM "PersonPrivacy" WHERE "PersonId"='{person}' ''').split(',')
try:
 sql(f'''UPDATE "PersonPrivacy" SET "DirectoryVisible"=true,"DisplayEmail"=false,"DisplayPhone"=false WHERE "PersonId"='{person}' ''')
 status, listing=z.req('/directory?regionId='+own[0]['id']+'&search='+member['number']); assert status==200
 row=next(m for m in listing['items'] if m['id']==member['id'])
 assert row['email'] is None and row['phone'] is None and row['region']==own[0]['name']
 assert z.req('/members/'+member['id']+'?directory=true')[0]==200
 sql(f'''UPDATE "PersonPrivacy" SET "DirectoryVisible"=false WHERE "PersonId"='{person}' ''')
 assert member['id'] not in json.dumps(z.req('/directory?regionId='+own[0]['id']+'&search='+member['number'])[1])
 assert z.req('/members/'+member['id']+'?directory=true')[0]==404
finally:
 sql(f'''UPDATE "PersonPrivacy" SET "DirectoryVisible"={prior[0]},"DisplayEmail"={prior[1]},"DisplayPhone"={prior[2]} WHERE "PersonId"='{person}' ''')
status, body, headers=a.request('/membership/export','POST',json.dumps(dict(format='csv',columns=['name','club'],regionId=other['id'])).encode(),{'Content-Type':'application/json'})
assert status==200 and own[0]['name'].encode() not in body
print(json.dumps(dict(regions=len(regions),hierarchyDetails='passed',clubOptions='passed',crossRegionAccess='blocked',directoryOptOut='passed',contactPrivacy='passed',exportFilter='passed'),indent=2))
