"""Seed and measure a separate disposable 100k-member performance database."""
import json,os,pathlib,sys,time
from support import Client,sql,BASE
database=os.environ['CLUBS_TEST_DATABASE'];assert database.startswith('clubs_impact_members_perf_')
if '--seed' in sys.argv:
 assert sql('''SELECT count(*) FROM "Memberships" WHERE "Number" LIKE 'PERF-%' ''')=='0'
 commands=['BEGIN','SET LOCAL statement_timeout=0',"CREATE TEMP TABLE perf_ids AS SELECT i,md5('impact-perf-person-'||i)::uuid AS person,md5('impact-perf-membership-'||i)::uuid AS membership FROM generate_series(1,100000) i"]
 for table,changes in [
  ('Persons',"'Id',n.person,'UserId',NULL,'LegalName','Performance Member '||lpad(n.i::text,6,'0'),'DisplayName','Performance Member '||lpad(n.i::text,6,'0'),'SearchName','performance member '||lpad(n.i::text,6,'0'),'Photo',NULL"),
  ('PersonPrivacy',"'PersonId',n.person"),
  ('Memberships',"'Id',n.membership,'PersonId',n.person,'Number','PERF-'||lpad(n.i::text,9,'0'),'JoinedAt',now()-interval '60 days','CreatedAt',now()-interval '60 days','Status','Active','EndsAt',NULL"),
  ('MembershipHistory',"'Id',md5('impact-perf-history-'||n.i)::uuid,'MembershipId',n.membership,'NewStatus','Active','EffectiveAt',now()-interval '60 days','RecordedAt',now()-interval '60 days','EndsAt',NULL")]:
  commands.append(f'''INSERT INTO "{table}" SELECT r.* FROM (SELECT * FROM "{table}" LIMIT 1) p CROSS JOIN perf_ids n CROSS JOIN LATERAL jsonb_populate_record(NULL::"{table}",to_jsonb(p)||jsonb_build_object({changes})) r''')
 commands+=['COMMIT','ANALYZE "Persons"','ANALYZE "Memberships"','ANALYZE "MembershipHistory"','SELECT count(*) FROM "Memberships"']
 print(sql(';'.join(commands)));sys.exit()
assert BASE=='http://127.0.0.1:5096'
c=Client();assert c.login('admin@example.test')[0]==200
checks=[]
for name,path in [('first page','/members?size=20'),('number sort','/members?sort=number&size=20'),('last page','/members?sort=number&page=5000&size=20'),('name search','/members?search=Performance%20Member%20050000&size=20'),('member ID search','/members?search=PERF-000050000'),('6-month intelligence','/membership/metrics?range=6m'),('12-month intelligence','/membership/metrics?range=12m')]:
 at=time.perf_counter();status,data=c.req(path);elapsed=round((time.perf_counter()-at)*1000)
 checks.append(dict(scenario=name,status=status,elapsedMs=elapsed,recordsReturned=len(data.get('items',[])) if isinstance(data,dict) else None,passed=status==200 and ('items' not in data or len(data['items'])<=20)))
 print(name,status,elapsed,flush=True)
out=dict(database=database,membershipCount=int(sql('SELECT count(*) FROM "Memberships"')),syntheticMemberships=100000,tests=checks,passed=all(c['passed'] for c in checks),limits='Single local request timings; no concurrent production load claim.')
pathlib.Path('docs/qa/members-performance-results.json').write_text(json.dumps(out,indent=2));sys.exit(0 if out['passed'] else 1)
