"""Run only against an isolated communication QA database on port 5095."""
import os, uuid, json
from support import Client, sql, BASE

assert BASE == 'http://127.0.0.1:5095'
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_comms_test_')
members = ['club1', 'club2', 'zone2', 'district2', 'region2']
clients = {}
for name in members:
    c = Client(); assert c.login('member.' + name + '@example.test')[0] == 200
    clients[name] = c
results = []
for role, total, reachable in [('club',5,1), ('zonal',25,2), ('district',125,3), ('regional',625,4), ('national',3125,5), ('international',3125,5)]:
    c = Client(); assert c.login(role+'.president@example.test')[0] == 200
    status, scope = c.req('/communications/member-scope')
    assert status == 200 and scope['totalMembers'] == total and scope['reachableMembers'] == reachable, scope
    payload = dict(orgId=scope['id'], title='QA '+role+' '+uuid.uuid4().hex[:8], body='Hello {{member_name}}, a hierarchy test.', requestKey=str(uuid.uuid4()))
    status, sent = c.req('/communications/member-messages','POST',payload)
    assert status == 200 and sent['delivered'] == reachable and sent['total'] == total, (status,sent)
    assert c.req('/communications/member-messages','POST',payload)[1] == sent
    assert c.req('/communications/member-messages','POST',{**payload,'body':'Changed message'})[0] == 409
    for i, name in enumerate(members):
        status, inbox = clients[name].req('/communications/announcements')
        assert status == 200, inbox
        assert (payload['title'] in json.dumps(inbox)) == (i < reachable), (role,name,inbox)
    root = sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'IMPACT-HQ\'')
    if role != 'international':
        assert c.req('/communications/member-scope?orgId='+root)[0] in (403,404)
        assert c.req('/communications/member-messages','POST',{**payload,'orgId':root,'requestKey':str(uuid.uuid4())})[0] in (403,404)
    assert c.req('/communications/member-messages')[0] == 200
    results.append(dict(role=role, members=total, delivered=reachable, visibility='passed', retry='passed', scope='passed'))
assert clients['club1'].req('/communications/member-messages','POST',payload)[0] == 403
member = clients['club2']
assert member.req('/communications/preferences','PUT',dict(channel='InApp',transactional=False,marketing=False))[0] in (200,204)
zonal = Client(); assert zonal.login('zonal.president@example.test')[0] == 200
scope = zonal.req('/communications/member-scope')[1]
assert scope['reachableMembers'] == 1
payload = dict(orgId=scope['id'],title='QA consent '+uuid.uuid4().hex[:8],body='Consent test',requestKey=str(uuid.uuid4()))
status, sent = zonal.req('/communications/member-messages','POST',payload)
assert status == 200 and sent['delivered'] == 1
assert payload['title'] not in json.dumps(member.req('/communications/announcements')[1])
assert member.req('/communications/announcements/'+sent['id']+'/acknowledge','POST',{})[0] == 404
assert member.req('/communications/preferences','PUT',dict(channel='InApp',transactional=True,marketing=False))[0] in (200,204)
print(json.dumps(results,indent=2))
