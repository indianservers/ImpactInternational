"""Exercise setup and hierarchy mutations only in a disposable Impact QA database."""
import os
import uuid
from support import Client, sql

db = os.environ['CLUBS_TEST_DATABASE']
assert db.startswith('clubs_impact_structure_test_'), 'Never run these fixtures on the active installation'
c = Client()
assert c.login('admin@example.test')[0] == 200
code, structure = c.req('/structure')
assert code == 200, (code, structure)
kinds = ['National', 'Region', 'District', 'Zone', 'Club']
levels = {l['name']: l for l in structure['levels']}
assert set(levels) == {'International', *kinds}
assert not structure['setupReady'] and not structure['setupCompleted']
assert c.req('/setup-guide/complete', 'POST')[0] == 400
assert c.req('/levels', 'POST', {'name': 'Another Organization'})[0] == 409
root = structure['root']
parent = root
created = {}
for kind in kinds:
    level = levels[kind]
    assert level['parentLevelId'] == parent['levelId']
    payload = {'name': 'QA ' + kind, 'code': 'QA-' + kind.upper(),
               'levelId': level['id'], 'parentId': parent['id'],
               'country': 'India', 'city': 'Hyderabad', 'status': 'Active', 'version': 1}
    status, result = c.req('/organizations', 'POST', payload)
    assert status == 200, (kind, status, result)
    assert result['status'] == 'Active', 'Platform administrator can activate clubs directly'
    created[kind] = result
    status, rows = c.req('/organizations?levelId=' + level['id'])
    assert status == 200 and any(x['id'] == result['id'] for x in rows['items'])
    parent = result
assert c.req('/structure')[1]['setupReady']
assert c.req('/setup-guide/complete', 'POST')[0] == 204
assert c.req('/structure')[1]['setupCompleted']
assert c.req('/setup-guide/complete', 'POST')[0] == 204, 'Completion is idempotent'
wrong_parent = {**created['Zone'], 'name': 'Invalid zone', 'code': 'QA-INVALID', 'parentId': root['id']}
assert c.req('/organizations', 'POST', wrong_parent)[0] == 400
assert c.req('/organizations', 'POST', {**root, 'name': 'Second organization', 'code': 'QA-SECOND'})[0] == 400
club = created['Club']
assert c.req('/organizations/' + club['id'], 'PUT', {**club, 'name': 'QA Club Updated'})[0] == 200
assert c.req('/organizations/' + club['id'])[1]['name'] == 'QA Club Updated'

# An ordinary member cannot create units. The platform-admin flag confers full
# rights independently of the role's permission template.
user_id, assignment_id = str(uuid.uuid4()), str(uuid.uuid4())
role_id = sql('SELECT "Id" FROM "Roles" WHERE "Name"=\'Club Member\'', db)
sql(f'''INSERT INTO "Users" ("Id","Email","PasswordHash","Verified","PlatformAdmin","FailedAttempts")
  SELECT '{user_id}','structure-staff@example.test',"PasswordHash",true,false,0 FROM "Users" WHERE "Email"='admin@example.test';
  INSERT INTO "UserProfiles" ("Id","Name","Phone","Locale","TimeZone","Theme")
  VALUES ('{user_id}','Structure QA','','en','Asia/Kolkata','light');
  INSERT INTO "Assignments" ("Id","UserId","TenantId","RoleId","OrgId")
  VALUES ('{assignment_id}','{user_id}','{root['tenantId']}','{role_id}','{created['Zone']['id']}');''', db)
staff = Client()
assert staff.login('structure-staff@example.test')[0] == 200
payload = {**created['National'], 'name': 'QA National Two', 'code': 'QA-NATIONAL-2'}
assert staff.req('/organizations', 'POST', payload)[0] == 403
sql(f'UPDATE "Users" SET "PlatformAdmin"=true WHERE "Id"=\'{user_id}\'', db)
assert 'settings.manage' in staff.req('/workspaces')[1][0]['permissions']
assert staff.req('/organizations', 'POST', payload)[0] == 200
assert staff.req('/settings')[0] == 200
assert staff.req('/setup-guide/complete', 'POST')[0] == 204
assert staff.req('/organizations')[1]['total'] >= 7, 'Platform admin sees all levels even from a zone workspace'
assert int(sql('SELECT count(*) FROM "Tenants"', db)) == 1
print('PASS: International > National > Region > District > Zone > Club creation, parent validation, club management, persistent setup completion, and platform-admin permissions.')
