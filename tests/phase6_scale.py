"""Explicit isolated SQL fixture + actual API measurements; no production or external delivery."""
import os,sys,time,json,pathlib,subprocess,urllib.request,statistics,uuid
import support
DB='clubs_governance_scale';support.BASE='http://127.0.0.1:5087';sql=lambda q:support.sql(q,DB)
assert int(sql('SELECT count(*) FROM "Persons"'))>=100000,'Use the isolated prior-phase synthetic scale database only'
proc=None
try:
 env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':f'Host=127.0.0.1;Port=55432;Database={DB};Username=clubs','Logging__LogLevel__Default':'Warning'}
 log=pathlib.Path('.infra/phase6-scale-api.log').open('w');proc=subprocess.Popen(['dotnet','server/bin/Debug/net10.0/Clubs.Api.dll','--urls',support.BASE],env=env,stdout=log,stderr=log)
 for _ in range(100):
  try:urllib.request.urlopen(support.BASE+'/health',timeout=1);break
  except:time.sleep(.2)
 c=support.Client();assert c.login('admin@example.test')[0]==200;w=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(w)
 org=sql("SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='GC-006'");person=sql("SELECT \"Id\" FROM \"Persons\" WHERE \"UserId\"=(SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='member@example.test')")
 def good(path,body):
  code,b=c.req(path,'POST',body);assert code==200,(path,code,b);return b
 if '--fixture' in sys.argv:
  tag=uuid.uuid4().hex[:8]
  project=good('/projects',{'orgId':org,'name':'SCALE Operations '+tag,'managerId':person,'startsAt':'2025-01-01T00:00:00Z','endsAt':'2028-01-01T00:00:00Z','plannedVolunteers':50000})
  sql(f"UPDATE \"ServiceProjects\" SET \"Status\"='Planning' WHERE \"Id\"='{project['id']}'")
  meeting=good('/meetings',{'orgId':org,'title':'SCALE meeting '+tag,'startsAt':'2026-11-15T10:00:00Z','endsAt':'2026-11-15T12:00:00Z'})
  shift=good('/volunteers/shifts',{'projectId':project['id'],'title':'SCALE shift '+tag,'startsAt':'2026-12-01T10:00:00Z','endsAt':'2026-12-01T12:00:00Z','deadline':'2026-11-30T00:00:00Z','supervisorId':person,'capacity':10000})
  # Clone fully valid column shapes, explicitly labeled benchmark SQL status/verification setup.
  sql(f"""INSERT INTO "ServiceProjects" SELECT r.* FROM "ServiceProjects" p CROSS JOIN generate_series(1,2999) n CROSS JOIN LATERAL jsonb_populate_record(NULL::"ServiceProjects",to_jsonb(p)||jsonb_build_object('Id',gen_random_uuid(),'Name','SCALE project {tag} '||n)) r WHERE p."Id"='{project['id']}';
  INSERT INTO "ClubMeetings" SELECT r.* FROM "ClubMeetings" p CROSS JOIN generate_series(1,19999) n CROSS JOIN LATERAL jsonb_populate_record(NULL::"ClubMeetings",to_jsonb(p)||jsonb_build_object('Id',gen_random_uuid(),'Title','SCALE meeting {tag} '||n,'StartsAt',p."StartsAt"+(n%365)*interval '1 day','EndsAt',p."EndsAt"+(n%365)*interval '1 day')) r WHERE p."Id"='{meeting['id']}';
  INSERT INTO "VolunteerShifts" SELECT r.* FROM "VolunteerShifts" p CROSS JOIN generate_series(1,4) n CROSS JOIN LATERAL jsonb_populate_record(NULL::"VolunteerShifts",to_jsonb(p)||jsonb_build_object('Id',gen_random_uuid(),'Title','SCALE group {tag} '||n)) r WHERE p."Id"='{shift['id']}';
  CREATE TEMP TABLE fixture_people AS SELECT p."Id",row_number() over(ORDER BY p."Id") n FROM "Persons" p WHERE EXISTS(SELECT 1 FROM "Memberships" m WHERE m."PersonId"=p."Id" AND m."TenantId"='{w['tenantId']}') ORDER BY p."Id" LIMIT 50000;
  CREATE TEMP TABLE fixture_shifts AS SELECT "Id",row_number() over(ORDER BY "Id") n FROM "VolunteerShifts" WHERE "ProjectId"='{project['id']}';
  INSERT INTO "VolunteerAssignments"("Id","TenantId","OrgId","Version","UpdatedAt","ShiftId","PersonId","Status","BreakMinutes") SELECT gen_random_uuid(),'{w['tenantId']}','{org}',1,now(),s."Id",p."Id",'Completed',0 FROM fixture_people p JOIN fixture_shifts s ON s.n=(p.n-1)%5+1;
  INSERT INTO "VolunteerHours"("Id","TenantId","OrgId","Version","UpdatedAt","AssignmentId","PersonId","ProjectId","StartsAt","EndsAt","BreakMinutes","Hours","Reason","Status","SubmittedBy","VerifiedBy") SELECT gen_random_uuid(),a."TenantId",a."OrgId",1,now(),a."Id",a."PersonId",'{project['id']}',timestamptz '2025-02-01 10:00:00+00'+n*interval '1 day',timestamptz '2025-02-01 12:00:00+00'+n*interval '1 day',0,2,'SCALE synthetic verified log','Verified','{w['userId'] if 'userId' in w else sql("SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='admin@example.test'")}',NULL FROM "VolunteerAssignments" a CROSS JOIN generate_series(1,5) n WHERE a."ShiftId" IN(SELECT "Id" FROM fixture_shifts);""")
  code,doc=c.upload('/documents','scale.pdf',b'%PDF-1.4\nFictional scale metadata seed\n%%EOF',{'orgId':org,'title':'SCALE document '+tag,'visibility':'Organization'},'application/pdf');assert code==200,doc
  sql(f"""INSERT INTO "GovernanceDocuments" SELECT r.* FROM "GovernanceDocuments" p CROSS JOIN generate_series(1,9999) n CROSS JOIN LATERAL jsonb_populate_record(NULL::"GovernanceDocuments",to_jsonb(p)||jsonb_build_object('Id',gen_random_uuid(),'Title','SCALE document {tag} '||n)) r WHERE p."Id"='{doc['id']}'; ANALYZE;""")
 if '--tasks' in sys.argv:
  task=good('/tasks',{'orgId':org,'title':'SCALE task history '+uuid.uuid4().hex[:8],'description':'Synthetic completed action history','personId':person})
  sql(f"""UPDATE \"OperationTasks\" SET \"Status\"='Completed' WHERE \"Id\"='{task['id']}'; INSERT INTO \"OperationTasks\" SELECT r.* FROM \"OperationTasks\" t CROSS JOIN generate_series(1,49999) n CROSS JOIN LATERAL jsonb_populate_record(NULL::\"OperationTasks\",to_jsonb(t)||jsonb_build_object('Id',gen_random_uuid(),'Title','SCALE completed action '||n)) r WHERE t.\"Id\"='{task['id']}';ANALYZE;""")
 paths=['/meetings?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z','/projects','/volunteers/hours','/documents','/tasks','/operations/analytics'];measurements={}
 for path in paths:
  assert c.req(path)[0]==200;times=[]
  for _ in range(40):
   start=time.perf_counter();code,b=c.req(path);assert code==200,(path,code,b);times.append((time.perf_counter()-start)*1000)
  times.sort();measurements[path]={'samples':40,'p50_ms':round(statistics.median(times),2),'p95_ms':round(times[37],2),'p99_ms':round(times[-1],2),'errors':0}
 tables=['Persons','Orgs','ClubMeetings','ServiceProjects','VolunteerAssignments','VolunteerHours','GovernanceDocuments','OperationTasks'];counts={t:int(sql(f'SELECT count(*) FROM "{t}"')) for t in tables}
 plans={}
 for name,q in {'meetings':f'SELECT "Id" FROM "ClubMeetings" WHERE "TenantId"=\'{w["tenantId"]}\' AND "OrgId"=\'{org}\' AND "StartsAt">=\'2026-11-01\' AND "StartsAt"<\'2026-12-01\' ORDER BY "StartsAt" LIMIT 250','hours':f'SELECT sum("Hours") FROM "VolunteerHours" WHERE "TenantId"=\'{w["tenantId"]}\' AND "OrgId"=\'{org}\' AND "Status"=\'Verified\'','documents':f'SELECT "Id" FROM "GovernanceDocuments" WHERE "TenantId"=\'{w["tenantId"]}\' AND "OrgId"=\'{org}\' ORDER BY "Id" LIMIT 100'}.items():plans[name]=json.loads(sql('EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) '+q))[0]
 out={'counts':counts,'api':measurements,'conditions':'Isolated PostgreSQL 17.6 prior-phase 100k-person/10k-org clone. Existing fixture includes 20k meetings, 3k projects, 50k volunteer assignments, 250k hour entries and 10k document metadata records. Optional --tasks adds 50k completed task records. Use --fixture only for explicit initial fixture creation; the default is read-only measurement. SQL fixture approvals/verified hours are synthetic benchmark setup, not application review evidence. Document fixture measures metadata listing; only its seed object was uploaded, and cloned metadata does not claim 10k distinct download files. Forty warm loopback API reads after one warmup; shared local Windows machine. No external delivery or production SLO claim.'}
 pathlib.Path('test-results/phase6-scale.json').write_text(json.dumps(out,indent=2));pathlib.Path('test-results/phase6-explain.json').write_text(json.dumps(plans,indent=2));print(json.dumps(out,indent=2),flush=True)
finally:
 if proc:proc.terminate();proc.wait(timeout=20)
