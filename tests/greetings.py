"""Greeting setup, approval, personalization, opt-out and yearly worker matching."""
from support import Client, sql
import datetime, json, pathlib, time, uuid

results = []
def check(name, ok):
    results.append({'test': name, 'passed': bool(ok)})
    print(('PASS ' if ok else 'FAIL ') + name, flush=True)
    assert ok, name

def request(c, path, method='GET', body=None):
    code, result = c.req(path, method, body)
    assert code == 200, (path, code, result)
    return result

def login(email, role, org=None):
    c = Client()
    request(c, '/auth/login', 'POST', {'email': email, 'password': __import__('support').PASSWORD})
    choices = request(c, '/workspaces')
    w = next(w for w in choices if w['role'] == role and (org is None or w['orgId'] == org)
             and (role != 'Association Super Admin' or w['association'].startswith('Global')))
    assert c.activate(w)[0] in (200, 204)
    return c, w

def until(fn):
    end = time.monotonic() + 35
    while time.monotonic() < end:
        value = fn()
        if value:
            return value
        time.sleep(.3)
    raise AssertionError('Greeting worker timed out')

admin, root = login('admin@example.test', 'Association Super Admin')
review, _ = login('secretary@example.test', 'Association Super Admin', root['orgId'])
club = sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'')
member, _ = login('member@example.test', 'Club Member', club)
uid = sql('SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\'')
pid = sql(f'SELECT "Id" FROM "Persons" WHERE "UserId"=\'{uid}\'')
old = json.loads(sql(f'SELECT row_to_json(p) FROM (SELECT "BirthDate", "Anniversary" FROM "Persons" WHERE "Id"=\'{pid}\') p'))
privacy = sql(f'SELECT "BirthdayAnnouncements" FROM "PersonPrivacy" WHERE "PersonId"=\'{pid}\'')
prefs = next((p for p in request(member, '/communications/preferences') if p['channel'] == 'InApp'), None)
rules = []
tag = uuid.uuid4().hex[:8]
now = datetime.datetime.now(datetime.timezone.utc)
today = f'2000-{now.month:02}-{now.day:02}'
base = '/communications'

def draft(trigger, **overrides):
    body = {
        'rule': {'orgId': club, 'name': trigger + ' test ' + tag, 'trigger': trigger,
                 'templateId': str(uuid.UUID(int=0)), 'dailyLimit': 1000},
        'template': {'orgId': club, 'name': trigger + ' message ' + tag, 'channel': 'InApp',
                     'subject': 'Congratulations {{member_name}}!',
                     'body': 'Dear {{member_name}}, warm wishes from {{club_name}}. Member {{member_id}}.'},
    }
    body['rule'].update(overrides)
    return body

try:
    for trigger in ['Birthday', 'Anniversary']:
        invalid = draft(trigger, dailyLimit=0)
        before = sql(f'SELECT count(*) FROM "CommunicationTemplates" WHERE "Name"=\'{trigger} message {tag}\'')
        check('invalid greeting rejected', admin.req(base + '/greetings', 'POST', invalid)[0] == 400)
        check('failed setup rolls back its template', before == sql(f'SELECT count(*) FROM "CommunicationTemplates" WHERE "Name"=\'{trigger} message {tag}\''))
        rule = request(admin, base + '/greetings', 'POST', draft(trigger))
        rules.append(rule['id'])
        check(trigger + ' starts as inactive draft', rule['status'] == 'Draft' and not rule['active'])
        rule = request(admin, base + '/automations/' + rule['id'] + '/submit', 'POST', {})
        approval = request(admin, '/governance/requests/' + rule['approvalRequestId'])['request']
        request(review, '/governance/requests/' + approval['id'] + '/decisions', 'POST', {
            'decision': 'Approve', 'comment': 'Approve isolated greeting test', 'version': approval['version']})
    sql(f'UPDATE "Persons" SET "BirthDate"=\'{today}\', "Anniversary"=\'{today}\' WHERE "Id"=\'{pid}\'; UPDATE "PersonPrivacy" SET "BirthdayAnnouncements"=true WHERE "PersonId"=\'{pid}\'')
    request(member, base + '/preferences', 'PUT', {'channel': 'InApp', 'transactional': True, 'promotional': True, 'birthday': False})
    time.sleep(2)
    check('occasion opt-out blocks both greetings', all(sql(f'SELECT count(*) FROM "CommunicationExecutions" WHERE "AutomationId"=\'{r}\' AND "SourceKey" LIKE \'%:{pid}\'') == '0' for r in rules))
    request(member, base + '/preferences', 'PUT', {'channel': 'InApp', 'transactional': True, 'promotional': True, 'birthday': True})
    for rid in rules:
        mail = until(lambda: sql(f'SELECT "MailId" FROM "CommunicationExecutions" WHERE "AutomationId"=\'{rid}\' AND "SourceKey" LIKE \'%:{pid}\''))
        data = json.loads(sql(f'SELECT row_to_json(m) FROM (SELECT "Subject", "Body" FROM "Mails" WHERE "Id"=\'{mail}\') m'))
        check('subject and message personalized', '{{' not in data['Subject'] + data['Body'] and 'warm wishes' in data['Body'])
        until(lambda: sql(f'SELECT "DeliveryStatus" FROM "Mails" WHERE "Id"=\'{mail}\'') == 'Delivered')
    time.sleep(2)
    check('one greeting per occasion per member per day', all(sql(f'SELECT count(*) FROM "CommunicationExecutions" WHERE "AutomationId"=\'{r}\' AND "SourceKey" LIKE \'%:{pid}\'') == '1' for r in rules))
finally:
    for rid in rules:
        sql(f'UPDATE "CommunicationAutomations" SET "Active"=false WHERE "Id"=\'{rid}\'')
    values = {key: "NULL" if value is None else "'" + value + "'" for key, value in old.items()}
    sql(f'UPDATE "Persons" SET "BirthDate"={values["BirthDate"]}, "Anniversary"={values["Anniversary"]} WHERE "Id"=\'{pid}\'; UPDATE "PersonPrivacy" SET "BirthdayAnnouncements"={"true" if privacy == "t" else "false"} WHERE "PersonId"=\'{pid}\'')
    if prefs:
        request(member, base + '/preferences', 'PUT', prefs)
    pathlib.Path('test-results').mkdir(exist_ok=True)
    pathlib.Path('test-results/greetings.json').write_text(json.dumps(results, indent=2))
