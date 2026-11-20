"""Historical offices and Telugu preference checks. Use an isolated migrated clone only."""
import os
from support import Client, sql

assert os.environ.get('CLUBS_TEST_DATABASE', '').startswith('clubs_ui_history_te_'), 'Refusing a live database'
c = Client()
assert c.login('admin@example.test')[0] == 200
levels = {x['name']: x['id'] for x in c.req('/levels')[1]}
counts = {name: sql('SELECT count(*) FROM "' + name + '"') for name in ['Users', 'Assignments', 'Memberships']}
for kind in ['International', 'National', 'Region', 'Club']:
    org = c.req('/organizations?size=1&levelId=' + levels[kind])[1]['items'][0]['id']
    path = '/organizations/' + org + '/office-service-records'
    payload = dict(name='QA Former ' + kind + ' President', officeTitle='President', isPresident=True,
                   startYear=2018, endYear=2019, notes='Isolated regression fixture')
    status, record = c.req(path, 'POST', payload)
    assert status == 200, (status, record)
    assert any(x['id'] == record['id'] for x in c.req(path + '?presidentsOnly=true&year=2019')[1]['items'])
    assert all(x['id'] != record['id'] for x in c.req(path + '?year=2017')[1]['items'])
    update = dict(payload, endYear=2020, version=record['version'])
    status, revised = c.req(path + '/' + record['id'], 'PUT', update)
    assert status == 200, (status, revised)
    assert c.req(path + '/' + record['id'], 'PUT', update)[0] == 409
    assert c.req(path + '/' + record['id'], 'PUT', dict(update, endYear=2099, version=revised['version']))[0] == 400
    assert c.req(path + '/' + record['id'] + '/archive', 'POST', dict(version=revised['version'], reason='Regression cleanup'))[0] == 204
    assert sql('SELECT "Archived" FROM "HistoricalOfficeRecords" WHERE "Id"=\'' + record['id'] + '\'') == 't'
    print(kind + ': persisted, year-filtered, edited, version-checked and archived')

member = Client()
assert member.login('member.club1@example.test')[0] == 200
assert member.req(path, 'POST', payload)[0] == 403
president = Client()
assert president.login('club.president@example.test')[0] == 200
other = Client()
assert other.login('member.club2@example.test')[0] == 200
other_org = other.req('/workspaces')[1][0]['orgId']
assert president.req('/organizations/' + other_org + '/office-service-records')[0] in [403, 404]
for locale in ['te', 'en']:
    assert c.req('/users/language', 'PUT', dict(locale=locale))[0] == 204
    assert c.req('/auth/me')[1]['locale'] == locale
assert c.req('/users/language', 'PUT', dict(locale='unsupported'))[0] == 400
assert counts == {name: sql('SELECT count(*) FROM "' + name + '"') for name in counts}
print('Permission isolation, language persistence, and absence of new access grants passed')
