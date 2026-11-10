"""Opt-in synthetic governance scale fixture in clubs_governance_scale only.
Clone clubs_scale_phase2, migrate and --seed-phase3, then use --fixture once.
"""
from support import sql,Client
import os,sys,json,time,statistics,pathlib
DB='clubs_governance_scale'
def run(q):return sql(q,DB)
if '--fixture' in sys.argv:
 assert run('SELECT count(*) FROM "OfficerAppointments"')=='0','Use a fresh isolated scale clone'
 run('''BEGIN; SET LOCAL statement_timeout='300s';
 INSERT INTO "Memberships" SELECT (jsonb_populate_record(NULL::"Memberships",to_jsonb(m)||jsonb_build_object('Id',md5('gov-scale-membership-'||n)::uuid,'PersonId',md5('scale-person-'||n)::uuid,'ClubId',o."Id",'TenantId',o."TenantId",'CategoryId',c."Id",'Number','GOV-SCALE-'||lpad(n::text,7,'0'),'SourceApplicationId',null,'ImportRowId',null,'Status','Active','EndsAt',null))).*
 FROM generate_series(1,100000)n JOIN "Orgs" o ON o."Code"='SCALE-'||(((n-1+17)%10000)+1)
 CROSS JOIN (SELECT * FROM "Memberships" LIMIT 1)m JOIN "MembershipCategories" c ON c."TenantId"=o."TenantId" AND c."Code"='REG';
 INSERT INTO "OfficerAppointments" ("Id","TenantId","OrgId","PersonId","PositionId","TermId","MembershipId","GrantRoleId","Method","Status","StartsAt","EndsAt","EffectiveAt","ProposerId","ApproverId","DocumentId","Notes","Version","CreatedAt","PositionTitle","OrganizationName")
 SELECT md5('gov-history-'||n)::uuid,o."TenantId",o."Id",md5('scale-person-'||n)::uuid,p."Id",NULL,NULL,NULL,'Appointed','Completed',t.start,t.start+interval '15 days',t.start,u."Id",NULL,NULL,'Synthetic scale fixture',1,t.start,p."Title",o."Name"
 FROM generate_series(1,50000)n JOIN "Orgs" o ON o."Code"='SCALE-'||(((n-1)%10000)+1)
 JOIN LATERAL(SELECT * FROM "OfficerPositions" WHERE "TenantId"=o."TenantId" AND "LevelId"=o."LevelId" ORDER BY "Code" LIMIT 1)p ON true
 CROSS JOIN(SELECT "Id" FROM "Users" WHERE "Email"='admin@example.test')u
 CROSS JOIN LATERAL(SELECT timestamptz '2026-07-01'+((n-1)/10000)*interval '15 days' AS start)t;
 INSERT INTO "OfficerAppointments" ("Id","TenantId","OrgId","PersonId","PositionId","TermId","MembershipId","GrantRoleId","Method","Status","StartsAt","EndsAt","EffectiveAt","ProposerId","ApproverId","DocumentId","Notes","Version","CreatedAt","PositionTitle","OrganizationName")
 SELECT md5('gov-current-'||n)::uuid,o."TenantId",o."Id",md5('scale-person-'||n)::uuid,p."Id",NULL,md5('scale-membership-'||n)::uuid,NULL,'Appointed','Active',timestamptz '2026-10-01',timestamptz '2027-10-01',timestamptz '2026-10-01',u."Id",NULL,NULL,'Synthetic scale fixture',1,now(),p."Title",o."Name"
 FROM generate_series(1,5000)n JOIN "Orgs" o ON o."Code"='SCALE-'||n
 JOIN LATERAL(SELECT * FROM "OfficerPositions" WHERE "TenantId"=o."TenantId" AND "LevelId"=o."LevelId" ORDER BY "Code" LIMIT 1)p ON true CROSS JOIN(SELECT "Id" FROM "Users" WHERE "Email"='admin@example.test')u;
 INSERT INTO "OfficerAppointmentHistory" ("Id","TenantId","AppointmentId","Status","Detail","ActorId","At") SELECT gen_random_uuid(),"TenantId","Id","Status",'Synthetic benchmark provenance',NULL,"CreatedAt" FROM "OfficerAppointments";
 INSERT INTO "Committees" ("Id","TenantId","OrgId","Name","Code","Description","Purpose","Category","Notes","StartsAt","EndsAt","Status","Version")
 SELECT md5('gov-committee-'||n)::uuid,o."TenantId",o."Id",'Scale Committee '||n,'SC-'||n,'Synthetic benchmark fixture','Coordinate community service','Service','',timestamptz '2026-10-01',timestamptz '2027-10-01','Active',1 FROM generate_series(1,5000)n JOIN "Orgs" o ON o."Code"='SCALE-'||n;
 INSERT INTO "CommitteeMemberships" ("Id","TenantId","CommitteeId","PersonId","RoleId","Responsibilities","StartsAt","EndsAt","Status","Version")
 SELECT md5('gov-committee-member-'||n)::uuid,c."TenantId",c."Id",md5('scale-person-'||((((n-1)%5000)+1)+((n-1)/5000)*10000))::uuid,r."Id",'Synthetic service responsibility',timestamptz '2026-10-01',timestamptz '2027-10-01','Active',1
 FROM generate_series(1,25000)n JOIN "Committees" c ON c."Id"=md5('gov-committee-'||(((n-1)%5000)+1))::uuid JOIN LATERAL(SELECT "Id" FROM "CommitteeRoles" WHERE "TenantId"=c."TenantId" AND "CanManage"=(n<=5000) ORDER BY "Name" LIMIT 1)r ON true;
 INSERT INTO "GovernanceRequests" ("Id","TenantId","OrgId","Kind","Title","Payload","Status","CreatorId","SubjectUserId","WorkflowId","CurrentSequence","Revision","AppointmentId","CommitteeId","HandoverId","ResolutionId","DocumentId","ResultId","Version","CreatedAt","UpdatedAt")
 SELECT md5('gov-request-'||n)::uuid,o."TenantId",o."Id",'ClubUpdate','Synthetic club governance request',jsonb_build_object('name',o."Name",'code',o."Code",'parentId',o."ParentId",'levelId',o."LevelId",'version',o."Version")::text,'Submitted',u."Id",NULL,w."Id",1,1,NULL,NULL,NULL,NULL,NULL,NULL,1,now(),now()
 FROM generate_series(1,3000)n JOIN "Orgs" o ON o."Code"='SCALE-'||n JOIN "MembershipWorkflows" w ON w."TenantId"=o."TenantId" AND w."Kind"='Governance' AND w."Active" CROSS JOIN(SELECT "Id" FROM "Users" WHERE "Email"='admin@example.test')u;
 ANALYZE; COMMIT;''')
counts={t:int(run(f'SELECT count(*) FROM "{t}"')) for t in ['Persons','Memberships','OfficerAppointments','OfficerAppointmentHistory','Committees','CommitteeMemberships','GovernanceRequests']}
assert counts['OfficerAppointments']>=55000 and counts['Memberships']>=200000
c=Client();assert c.login('admin@example.test')[0]==200;w=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(w)
measurements={}
for path in ['/clubs?size=20','/clubs?size=20&search=Scale%20Club%20999','/officer-appointments?status=Current','/officer-appointments?status=Completed','/committees','/governance/requests','/administration/dashboard']:
 values=[]
 for i in range(31):
  started=time.perf_counter();code,body=c.req(path);assert code==200,(path,code,body)
  if i:values.append((time.perf_counter()-started)*1000)
 measurements[path]={'samples':len(values),'median_ms':round(statistics.median(values),2),'p95_ms':round(sorted(values)[28],2)}
tenant=w['tenantId'];root=w['orgId']
queries={
 'current_leadership':f'''SELECT a."Id",a."PositionTitle",p."DisplayName" FROM "OfficerAppointments" a JOIN "Persons" p ON p."Id"=a."PersonId" WHERE a."TenantId"='{tenant}' AND a."Status"='Active' AND a."StartsAt"<=now() AND a."EffectiveAt"<=now() AND a."EndsAt">now() ORDER BY a."EndsAt",a."Id" LIMIT 20''',
 'committee_members':'''SELECT m."Id",m."PersonId",m."RoleId" FROM "CommitteeMemberships" m WHERE m."CommitteeId"=md5('gov-committee-100')::uuid AND m."Status"='Active' AND m."StartsAt"<=now() AND m."EndsAt">now() ORDER BY m."StartsAt" LIMIT 100''',
 'district_hierarchy':f'''WITH RECURSIVE tree AS(SELECT "Id","TenantId" FROM "Orgs" WHERE "TenantId"='{tenant}' AND "Code"='GC-004' UNION ALL SELECT o."Id",o."TenantId" FROM "Orgs" o JOIN tree t ON o."ParentId"=t."Id" AND o."TenantId"=t."TenantId") SELECT count(*) FROM tree'''}
plans={};sql_times={}
for name,q in queries.items():
 values=[]
 for i in range(31):
  p=json.loads(run('EXPLAIN(ANALYZE,BUFFERS,TIMING OFF,FORMAT JSON) '+q));plans[name]=p
  if i:values.append(p[0]['Execution Time'])
 sql_times[name]={'samples':len(values),'median_ms':round(statistics.median(values),2),'p95_ms':round(sorted(values)[28],2)}
report={'counts':counts,'api':measurements,'sql':sql_times,'conditions':'PostgreSQL 17.6, Windows local, isolated synthetic clone, warm sequential loopback HTTP, 30 samples after one warmup. 50,000 nonoverlapping historical terms plus 5,000 current terms. API measurements include authorization/projections/pagination. SQL uses EXPLAIN ANALYZE BUFFERS TIMING OFF, retains measured Execution Time. No concurrent user load or production deployment claim.'}
pathlib.Path('test-results/phase3-scale.json').write_text(json.dumps(report,indent=2));pathlib.Path('test-results/phase3-explain.json').write_text(json.dumps(plans,indent=2));print(json.dumps(report,indent=2))
