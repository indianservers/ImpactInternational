"""Explicitly opt-in isolated scale fixture and measured SQL/HTTP benchmark.
Create clubs_scale_phase2 as a clone of a disposable, seeded clubs_test database first.
Never point this fixture at an operational database.
"""
from support import sql, Client
import json,pathlib,statistics,time,re,sys
DB='clubs_scale_phase2'
def run(q):return sql(q,DB)
if '--fixture' in sys.argv:
 assert run('SELECT count(*) FROM "Persons" WHERE "DisplayName" LIKE \'Scale Person %\'')=='0','Fixture already installed'
 run('''BEGIN; SET statement_timeout='300s';
 INSERT INTO "Orgs" SELECT (jsonb_populate_record(NULL::"Orgs",to_jsonb(o)||jsonb_build_object('Id',md5('scale-club-'||n)::uuid,'Code','SCALE-'||n,'Name','Scale Club '||n))).*
 FROM generate_series(1,10000) n CROSS JOIN LATERAL (SELECT * FROM "Orgs" WHERE "Code"=CASE WHEN n<=9000 THEN 'GC-006' ELSE 'CF-002' END LIMIT 1) o;
 INSERT INTO "Persons" SELECT (jsonb_populate_record(NULL::"Persons",to_jsonb(p)||jsonb_build_object('Id',md5('scale-person-'||n)::uuid,'UserId',null,'LegalName','Scale Person '||lpad(n::text,6,'0'),'DisplayName','Scale Person '||lpad(n::text,6,'0'),'SearchName','scale person '||lpad(n::text,6,'0'),'Photo',null))).*
 FROM generate_series(1,100000) n CROSS JOIN (SELECT * FROM "Persons" LIMIT 1) p;
 INSERT INTO "PersonPrivacy" ("PersonId","DirectoryVisible","DisplayEmail","DisplayPhone","ProfessionalVisible","BirthdayAnnouncements","Marketing","PhotoVisible","AllowContactRequests","CardNameVisible","TransactionalEmail","Version")
 SELECT md5('scale-person-'||n)::uuid,true,false,false,false,false,false,false,true,false,true,1 FROM generate_series(1,100000) n;
 INSERT INTO "Memberships" SELECT (jsonb_populate_record(NULL::"Memberships",to_jsonb(m)||jsonb_build_object('Id',md5('scale-membership-'||n)::uuid,'PersonId',md5('scale-person-'||(((n-1)%100000)+1))::uuid,'ClubId',o."Id",'TenantId',o."TenantId",'CategoryId',c."Id",'Number','SCALE-'||lpad(n::text,7,'0'),'SourceApplicationId',null,'ImportRowId',null,'Status','Active','EndsAt',null))).*
 FROM generate_series(1,105000) n JOIN "Orgs" o ON o."Code"='SCALE-'||(((n-1+CASE WHEN n>100000 THEN 1 ELSE 0 END)%10000)+1)
 CROSS JOIN (SELECT * FROM "Memberships" LIMIT 1) m
 JOIN "MembershipCategories" c ON c."TenantId"=o."TenantId" AND c."Code"='REG';
 INSERT INTO "MembershipApplications" SELECT (jsonb_populate_record(NULL::"MembershipApplications",to_jsonb(a)||jsonb_build_object('Id',md5('scale-application-'||n)::uuid,'PersonId',md5('scale-person-'||n)::uuid,'InvitationId',null,'Status','Submitted'))).*
 FROM generate_series(1,3000) n CROSS JOIN (SELECT * FROM "MembershipApplications" LIMIT 1) a;
 ANALYZE; COMMIT;
 ''')
counts={t:int(run(f'SELECT count(*) FROM "{t}"')) for t in ['Tenants','Orgs','Persons','Memberships','MembershipApplications']}
counts['Clubs']=int(run('SELECT count(*) FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."IsClub"'))
counts['PendingApplications']=int(run("SELECT count(*) FROM \"MembershipApplications\" WHERE \"Status\" IN ('Submitted','Under Review')"))
assert counts['Persons']>=100000 and counts['Memberships']>=105000
tenant=run('SELECT "TenantId" FROM "Orgs" WHERE "Code"=\'GC-006\'')
root=run('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-001\'')
scope=f'''WITH RECURSIVE tree AS (SELECT "Id","TenantId" FROM "Orgs" WHERE "Id"='{root}' AND "TenantId"='{tenant}' UNION ALL SELECT o."Id",o."TenantId" FROM "Orgs" o JOIN tree t ON o."ParentId"=t."Id" AND o."TenantId"=t."TenantId") '''
visibility=f'''((EXISTS(SELECT 1 FROM "Orgs" o WHERE o."Id"='{root}' AND o."TenantId"='{tenant}' AND o."ParentId" IS NULL) AND NOT EXISTS(SELECT 1 FROM "Orgs" o WHERE o."TenantId"='{tenant}' AND o."ParentId" IS NULL AND o."Id"<>'{root}')))'''
base=f''' FROM "Memberships" m JOIN "Persons" p ON p."Id"=m."PersonId" WHERE m."TenantId"='{tenant}' AND {visibility}'''
queries={'member_page':scope+'SELECT m."Id",p."DisplayName",m."Number"'+base+' ORDER BY p."DisplayName",m."Id" LIMIT 25',
 'member_search':scope+'SELECT * FROM ('+'SELECT m."Id",p."DisplayName"'+base+''' AND p."SearchName" ILIKE '%scale person 0999%' UNION SELECT m."Id",p."DisplayName"'''+base+''' AND m."Number" ILIKE '%scale person 0999%') matches ORDER BY "DisplayName","Id" LIMIT 25''',
 'member_count':scope+f'''SELECT count(*) FROM "Memberships" m WHERE m."TenantId"='{tenant}' AND {visibility}''',
 'directory_count':scope+f'''SELECT count(*) FROM "Memberships" m WHERE m."TenantId"='{tenant}' AND {visibility} AND m."Status"='Active' AND (m."EndsAt" IS NULL OR m."EndsAt">now()) AND EXISTS(SELECT 1 FROM "PersonPrivacy" pr WHERE pr."PersonId"=m."PersonId" AND pr."DirectoryVisible")'''}
plans={};results={}
for name,q in queries.items():
 times=[]
 for i in range(31):
  plan=json.loads(run('EXPLAIN (ANALYZE, BUFFERS, TIMING OFF, FORMAT JSON) '+q));plans[name]=plan
  if i:times.append(plan[0]['Execution Time'])
 results[name]={'samples':len(times),'median_ms':round(statistics.median(times),2),'p95_ms':round(sorted(times)[28],2)}
c=Client();assert c.login('admin@example.test')[0]==200
w=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));assert c.activate(w)[0]==204
api={}
for path in ['/members?size=25','/members?size=25&search=scale%20person%200999','/directory?size=25','/members?size=25&sort=number']:
 times=[]
 for i in range(31):
  start=time.perf_counter();code,body=c.req(path);assert code==200,(code,body)
  if i:times.append((time.perf_counter()-start)*1000)
 api[path]={'samples':len(times),'median_ms':round(statistics.median(times),2),'p95_ms':round(sorted(times)[28],2)}
report={'counts':counts,'sql':results,'api':api,'conditions':'PostgreSQL 17.6, Windows local, warm sequential loopback HTTP, 30 measured requests per case after one warmup, no concurrent traffic; separate disposable database cloned from integration fixtures. SQL uses EXPLAIN ANALYZE BUFFERS TIMING OFF to avoid per-node clock instrumentation; Execution Time remains measured. Scope checks the sole-root invariant in SQL and falls back to recursive IDs for district/club workspaces. API includes authorization, count and field projection. Not a concurrent load test.'}
pathlib.Path('test-results').mkdir(exist_ok=True)
pathlib.Path('test-results/phase2-scale.json').write_text(json.dumps(report,indent=2))
pathlib.Path('test-results/phase2-explain.json').write_text(json.dumps(plans,indent=2))
print(json.dumps(report,indent=2))

