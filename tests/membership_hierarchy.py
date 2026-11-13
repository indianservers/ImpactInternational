"""Check cascading membership choices and scope in a disposable structure database."""
import os
import uuid
from support import Client, sql

db = os.environ['CLUBS_TEST_DATABASE']
assert db.startswith('clubs_impact_structure_test_')
admin = Client()
assert admin.login('admin@example.test')[0] == 200
structure = admin.req('/structure')[1]
levels = {l['name']: l for l in structure['levels']}
kinds = ['National', 'Region', 'District', 'Zone', 'Club']
tag = uuid.uuid4().hex[:7]
branches = []
for branch in ['A', 'B']:
    parent = structure['root']
    nodes = {}
    for kind in kinds:
        status, node = admin.req('/organizations', 'POST', {
            'name': f'Picker {branch} {kind} {tag}', 'code': f'P-{tag}-{branch}-{kind}',
            'levelId': levels[kind]['id'], 'parentId': parent['id'], 'status': 'Active',
            'country': 'India', 'version': 1,
        })
        assert status == 200, (kind, status, node)
        nodes[kind] = node
        parent = node
    branches.append(nodes)
for nodes in branches:
    parent = structure['root']
    for kind in kinds:
        status, data = admin.req(f'/membership/hierarchy?kind={kind}&parentId={parent["id"]}')
        assert status == 200
        assert nodes[kind]['id'] in {x['id'] for x in data['items']}
        assert all(x['parentId'] == parent['id'] for x in data['items'])
        assert all(set(x) == {'id', 'name', 'code', 'parentId'} for x in data['items'])
        parent = nodes[kind]
    status, data = admin.req('/membership/hierarchy?clubId=' + nodes['Club']['id'])
    assert status == 200
    path = {x['kind']: x['id'] for x in data['selectedPath']}
    assert all(path[kind] == nodes[kind]['id'] for kind in kinds)
assert admin.req('/membership/hierarchy?kind=Club&parentId=' + branches[0]['Region']['id'])[1]['items'] == []
assert admin.req('/membership/hierarchy?kind=Invalid')[0] == 400

# An assigned club member can see their own chain, without seeing other branches.
uid, aid = str(uuid.uuid4()), str(uuid.uuid4())
email = f'picker-{tag}@example.test'
role = sql('SELECT "Id" FROM "Roles" WHERE "Name"=\'Club Member\'', db)
sql(f'''INSERT INTO "Users" ("Id","Email","PasswordHash","Verified","PlatformAdmin","FailedAttempts")
SELECT '{uid}','{email}',"PasswordHash",true,false,0 FROM "Users" WHERE "Email"='admin@example.test';
INSERT INTO "UserProfiles" ("Id","Name","Phone","Locale","TimeZone","Theme")
VALUES ('{uid}','Picker QA','','en','Asia/Kolkata','light');
INSERT INTO "Assignments" ("Id","UserId","TenantId","RoleId","OrgId")
VALUES ('{aid}','{uid}','{structure['root']['tenantId']}','{role}','{branches[0]['Zone']['id']}');''', db)
member = Client()
assert member.login(email)[0] == 200
status, data = member.req('/membership/hierarchy?kind=National')
assert status == 200 and {x['id'] for x in data['items']} == {branches[0]['National']['id']}
assert member.req('/membership/hierarchy?kind=Club&parentId=' + branches[1]['Zone']['id'])[1]['items'] == []
assert member.req('/membership/hierarchy?clubId=' + branches[1]['Club']['id'])[0] == 404
assert member.req('/membership/hierarchy?clubId=' + branches[0]['Club']['id'])[0] == 200
status, public = member.req('/membership/hierarchy?publicCatalog=true&kind=Club&parentId=' + branches[1]['Zone']['id'])
assert status == 200 and public['items'][0]['id'] == branches[1]['Club']['id']
assert member.req('/membership/hierarchy?publicCatalog=true&tenantId=' + str(uuid.uuid4()))[0] == 404
print('PASS: parent-filtered lists, selected club ancestry, branch isolation, public discovery, and invalid-input checks.')
