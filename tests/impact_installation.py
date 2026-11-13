"""Verify the clean, dedicated Impact International installation.

Run against clubs_impact before entering real records. Creates login sessions only;
the write checks deliberately submit changes that must be rejected.
"""
import json
import os
from support import Client, sql

assert os.environ.get('CLUBS_TEST_DATABASE') == 'clubs_impact'
c = Client()
assert c.login('admin@example.test')[0] == 200
status, me = c.req('/auth/me')
assert status == 200 and me['workspaceId'], 'Login must select a workspace'
status, workspaces = c.req('/workspaces')
assert status == 200 and len(workspaces) == 1
w = workspaces[0]
assert w['association'] == 'Impact International' and w['id'] == me['workspaceId']
checks = []
for path in [
    '/branding', '/settings', '/dashboard', '/levels', '/roles', '/organizations',
    '/members', '/membership/options', '/membership/catalog', '/membership/me',
    '/governance/options', '/administration/dashboard', '/clubs', '/committees',
    '/events', '/my/invitations', '/my/registrations', '/my/finance',
    '/meetings?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z', '/projects', '/tasks', '/operations/options', '/operations/analytics',
    '/finance/entities', '/intelligence/dashboard', '/intelligence/metrics',
    '/intelligence/branding/current', '/intelligence/branding', '/id-cards/templates',
]:
    status, body = c.req(path)
    checks.append({'path': path, 'status': status})
    assert status == 200, (path, status, body)
for path in ['/associations', '/registration-requests']:
    status, body = c.req(path, 'POST', {
        'name': 'Second Organization', 'email': 'test@example.test',
        'description': 'This request must be rejected',
    })
    assert status == 409, (path, status, body)
settings = c.req('/settings')[1]
assert settings['name'] == 'Impact International'
assert settings['currency'] == 'INR' and settings['timeZone'] == 'Asia/Kolkata'
settings['name'] = 'Second Organization'
assert c.req('/settings', 'PUT', settings)[0] == 400
counts = {}
for table in [
    'Tenants', 'Orgs', 'Users', 'Persons', 'Memberships', 'Events', 'ClubMeetings',
    'ServiceProjects', 'OperationTasks', 'CommunicationMessages', 'Conversations',
    'FinanceInvoices', 'FinancePayments', 'MembershipApplications', 'RegistrationRequests',
    'GovernanceDocuments', 'GovernanceRequests', 'IssuedCertificates', 'DigitalCards', 'Mails',
]:
    count = int(sql(f'SELECT count(*) FROM "{table}"'))
    expected = 1 if table in ['Tenants', 'Orgs', 'Users'] else 0
    assert count == expected, (table, count, expected)
    counts[table] = count
assert len(c.req('/roles')[1]) >= 17
print(json.dumps({'defaultOrganization': w['association'], 'screens': checks,
                  'singleOrganizationGuards': 'passed', 'recordCounts': counts}, indent=2))
