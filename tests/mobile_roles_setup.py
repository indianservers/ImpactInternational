"""Create one isolated account per defined role in a disposable QA database."""
import json,uuid,pathlib,os
from support import sql
DB=os.environ['CLUBS_TEST_DATABASE']
assert DB.startswith('clubs_mobile_e2e_'), 'Role fixtures are restricted to disposable QA databases'
roles=json.loads(sql("""SELECT json_agg(x) FROM (SELECT r."Id" id,r."Name" name,r."TenantId" tenant FROM "Roles" r WHERE r."TenantId"=(SELECT "TenantId" FROM "Orgs" WHERE "Code"='GC-001') ORDER BY r."Name") x""",DB))
expected=['Association Super Admin','International Administrator','National Administrator','District Administrator','Regional Administrator','Zone Administrator','Club President','Club Secretary','Club Treasurer','Club Manager','Club Member','Guest / Applicant']
fixtures=[]
for i,name in enumerate(expected):
 r=next(r for r in roles if r['name']==name);email=f'mobile-role-{i}@example.test'
 existing=sql(f"SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='{email}'",DB)
 uid=existing or str(uuid.uuid4())
 code={'Association Super Admin':'GC-001','International Administrator':'GC-001','National Administrator':'GC-002','Regional Administrator':'GC-003','District Administrator':'GC-004','Zone Administrator':'GC-005'}.get(name,'GC-006')
 org=sql(f"SELECT \"Id\" FROM \"Orgs\" WHERE \"Code\"='{code}'",DB)
 if not existing:
  sql(f"""BEGIN; INSERT INTO "Users" ("Id","Email","PasswordHash","Verified","PlatformAdmin","FailedAttempts") SELECT '{uid}','{email}',"PasswordHash",true,false,0 FROM "Users" WHERE "Email"='admin@example.test'; INSERT INTO "UserProfiles" ("Id","Name","Phone","Locale","TimeZone","Theme") VALUES ('{uid}','Mobile QA {i}','','en','Asia/Kolkata','light'); INSERT INTO "Assignments" ("Id","UserId","TenantId","RoleId","OrgId") VALUES ('{uuid.uuid4()}','{uid}','{r['tenant']}','{r['id']}','{org}'); COMMIT;""",DB)
 fixtures.append({'email':email,'role':name,'orgId':org,'userId':uid})
pathlib.Path('.infra/mobile-role-fixtures.json').write_text(json.dumps(fixtures,indent=2))
print('Isolated role fixtures:',len(fixtures))
