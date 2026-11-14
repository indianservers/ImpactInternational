"""Read-only checks for simple Finance filtering and collections status semantics."""
import json, pathlib, sys
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from support import Client, BASE

assert BASE == 'http://127.0.0.1:5095', 'Use the isolated QA API.'
checks = []
def check(name, passed):
    checks.append({'name': name, 'passed': bool(passed)})
    assert passed, name

admin = Client()
check('Admin login', admin.login('saisatish@indianservers.com')[0] == 200)
options = admin.req('/finance/dues-center/options')[1]
club = next(o for o in options['organizations'] if o['name'] == 'Demo Region 01 District 01 Zone 01 Club 01')
foreign = next(o for o in options['organizations'] if o['name'] == 'Demo Region 02 District 01 Zone 01 Club 01')
url = '/finance/dues-center/collections?orgId=' + club['id']
code, verified = admin.req(url + '&status=Verified')
check('Verified register available', code == 200)
check('Verified rows carry payment references and club names', bool(verified['items']) and all(r['paymentId'] and r['club'] == club['name'] for r in verified['items']))
code, pending = admin.req(url + '&status=Submitted')
check('Awaiting-verification filter contains only submitted claims', code == 200 and bool(pending['items']) and all(r['status'] == 'Submitted' and not r['paymentId'] for r in pending['items']))
_, all_rows = admin.req(url)
check('Existing unfiltered register remains available', all_rows['total'] >= verified['total'] + pending['total'])
check('Currency filter excludes other currencies', admin.req(url + '&status=Verified&currency=USD')[1]['total'] == 0)
check('Invalid status rejected', admin.req(url + '&status=NotAStatus')[0] == 400)
check('Invalid currency rejected', admin.req(url + '&currency=INVALID')[0] == 400)
_, report = admin.req('/finance/dues-center/reports/club?orgId=' + club['id'])
check('Selected-club dues contain only selected club', len(report['items']) == 1 and report['items'][0]['groupId'] == club['id'])
_, members = admin.req('/finance/dues-center/reports/pending?orgId=' + club['id'])
check('Expanded pending invoices stay within selected club', bool(members['items']) and all(r['club'] == club['name'] and r['pending'] > 0 for r in members['items']))
code, body, headers = admin.request(url + '&status=Verified&format=csv')
check('Filtered CSV includes club column', code == 200 and 'Club' in body.decode('utf-8-sig').splitlines()[0])
president = Client()
check('Club president login', president.login('club.president@example.test')[0] == 200)
check('Club president sees own collections', president.req(url + '&status=Verified')[0] == 200)
check('Foreign-club collections blocked', president.req('/finance/dues-center/collections?orgId=' + foreign['id'] + '&status=Verified')[0] == 404)
member = Client()
check('Member login', member.login('member.club1@example.test')[0] == 200)
check('Ordinary member cannot access collection register', member.req(url + '&status=Verified')[0] == 403)
pathlib.Path('docs/qa/simple-finance').mkdir(parents=True, exist_ok=True)
pathlib.Path('docs/qa/simple-finance/api-checks.json').write_text(json.dumps(checks, indent=2), encoding='utf-8')
print(f'Passed {len(checks)} checks')
