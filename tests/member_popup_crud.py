"""Photo/social/ID CRUD regression against a disposable copy of the demo data."""
import base64
import os
import struct
import zlib
import uuid
from support import Client, sql

db = os.environ['CLUBS_TEST_DATABASE']
assert db.startswith('clubs_impact_crud_test_')
c = Client()
assert c.login('admin@example.test')[0] == 200
options = c.req('/membership/options')[1]
club, category = options['clubs'][0]['id'], options['categories'][0]['id']
def png_chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
size = 16
pixels = b''.join(b'\x00' + bytes([48, 43, 120]) * size for _ in range(size))
photo = b'\x89PNG\r\n\x1a\n' + png_chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 2, 0, 0, 0)) + png_chunk(b'IDAT', zlib.compress(pixels)) + png_chunk(b'IEND', b'')
tag = uuid.uuid4().hex[:8]
ids, numbers = [], []
for i in range(5):
    person = {'legalName': f'CRUD Member {tag} {i}', 'displayName': '',
              'socialLinks': {'Instagram': '@impact_demo_test', 'LinkedIn': 'https://www.linkedin.com/in/impact-demo-test'}}
    body = {'person': person, 'clubId': club, 'categoryId': category,
            'photoBase64': base64.b64encode(photo).decode() if i == 0 else None}
    status, created = c.req('/members', 'POST', body)
    assert status == 200, (status, created)
    assert created['number'].startswith('IM-')
    ids.append(created['id']); numbers.append(created['number'])
assert len(set(numbers)) == 5
status, detail = c.req('/members/' + ids[0])
assert status == 200 and detail['person']['hasPhoto']
assert detail['person']['name'].startswith('CRUD Member')
assert detail['person']['socialLinks']['Instagram'] == 'https://www.instagram.com/impact_demo_test'
status, data, headers = c.request('/members/' + ids[0] + '/photo')
assert status == 200 and data.startswith(b'\x89PNG')
assert c.req('/members/' + ids[0] + '/photo?directory=true')[0] == 404
person = detail['person']
person['displayName'] = 'Updated CRUD member'
person['socialLinks'] = {'X': '@impact_demo_test'}
assert c.req('/members/' + ids[0] + '/person', 'PUT', person)[0] == 204
read = c.req('/members/' + ids[0])[1]
assert read['person']['name'] == 'Updated CRUD member'
assert read['person']['socialLinks'] == {'X': 'https://x.com/impact_demo_test'}
assert c.upload('/members/' + ids[0] + '/photo', 'test.png', photo, content_type='image/png')[0] == 200
before = int(sql('SELECT count(*) FROM "Persons"', db))
for extra in [
    {'photoBase64': 'not-base64'},
    {'photoBase64': base64.b64encode(b'not an image').decode()},
    {'person': {'legalName': 'Invalid social ' + tag, 'displayName': '', 'socialLinks': {'Instagram': 'javascript:alert(1)'}}},
    {'person': {'legalName': 'Wrong host ' + tag, 'displayName': '', 'socialLinks': {'Instagram': 'https://example.com/profile'}}},
]:
    payload = {'person': {'legalName': 'Invalid input ' + tag, 'displayName': ''}, 'clubId': club, 'categoryId': category, **extra}
    assert c.req('/members', 'POST', payload)[0] == 400
assert int(sql('SELECT count(*) FROM "Persons"', db)) == before, 'Invalid photo/social data must not create a partial member'
membership = c.req('/members/' + ids[0])[1]['membership']
status, result = c.req('/members/' + ids[0] + '/status', 'POST', {'status': 'Terminated', 'reason': 'Disposable CRUD delete/termination test', 'version': membership['version']})
assert status == 200 and result['status'] == 'Terminated'
assert c.req('/members/' + ids[0])[1]['membership']['status'] == 'Terminated'
print('PASS: five automatically numbered members; optional fields; photo create/read/update and privacy; social create/read/update and invalid-input rollback; membership termination.')
