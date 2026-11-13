"""OTP integration checks with a loopback SMTP capture; no external email."""
import json, re, socketserver, threading, time, uuid
from support import Client, sql, BASE
assert BASE == 'http://127.0.0.1:5095'
import os
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_comms_test_')
mailbox = []
class SMTP(socketserver.StreamRequestHandler):
 def handle(self):
  def reply(s): self.wfile.write((s+'\r\n').encode()); self.wfile.flush()
  reply('220 localhost test SMTP')
  while True:
   line=self.rfile.readline().decode().strip()
   if not line: break
   if line.upper().startswith(('EHLO','HELO')): reply('250 localhost')
   elif line.upper()=='DATA':
    reply('354 Continue'); chunks=[]
    while True:
     row=self.rfile.readline()
     if row==b'.\r\n' or not row: break
     chunks.append(row)
    mailbox.append(b''.join(chunks).decode());
    if '--capture-only' in __import__('sys').argv:
     __import__('pathlib').Path('.infra/otp-capture.json').write_text(json.dumps(mailbox))
    reply('250 Accepted')
   elif line.upper()=='QUIT': reply('221 Bye'); break
   else: reply('250 OK')
server=socketserver.ThreadingTCPServer(('127.0.0.1',2526),SMTP)
threading.Thread(target=server.serve_forever,daemon=True).start()
if '--capture-only' in __import__('sys').argv:
 print('Local SMTP capture listening on 2526',flush=True)
 threading.Event().wait()
def request(client,identifier):
 status,result=client.req('/auth/otp/request','POST',dict(identifier=identifier)); assert status==200,(status,result)
 return result['challengeId']
def code_for(email):
 for _ in range(100):
  for mail in reversed(mailbox):
   if email in mail:
    found=re.search(r'code is (\d{6})',mail)
    if found:return found[1]
  time.sleep(.1)
 raise AssertionError('OTP email not captured for '+email)
def verify(c,challenge,code):return c.req('/auth/otp/verify','POST',dict(challengeId=challenge,code=code))
# Reset only disposable QA challenges between runs.
sql('DELETE FROM "LoginChallenges"')
c=Client(); challenge=request(c,'member.club1@example.test'); code=code_for('member.club1@example.test')
assert c.req('/auth/otp/request','POST',dict(identifier='member.club1@example.test'))[0]==429
assert verify(c,challenge,'000000')[0]==401
assert verify(c,challenge,code)[0]==200
assert c.req('/communications/announcements')[0]==200
assert verify(Client(),challenge,code)[0]==401
assert Client().login('member.club1@example.test')[0]==403
assert Client().login('admin@example.test')[0]==200
# Fresh active member without a login: provision only after proof of email ownership.
person=sql('SELECT m."PersonId" FROM "Memberships" m JOIN "Persons" p ON p."Id"=m."PersonId" WHERE p."UserId" IS NULL AND m."Status"=\'Active\' ORDER BY m."Id" LIMIT 1')
email='otp-'+uuid.uuid4().hex[:12]+'@example.test'; phone='9198'+str(uuid.uuid4().int)[:8]
sql(f'''DELETE FROM "PersonContacts" WHERE "PersonId"='{person}'; INSERT INTO "PersonContacts" ("Id","PersonId","Kind","Label","Value","NormalizedValue","Verified") VALUES ('{uuid.uuid4()}','{person}','Email','Personal','{email}','{email}',false),('{uuid.uuid4()}','{person}','Phone','Personal','+{phone}','{phone}',false)''')
c=Client(); challenge=request(c,'+'+phone)
assert sql(f'''SELECT "UserId" IS NULL FROM "Persons" WHERE "Id"='{person}' ''')=='t'
assert verify(c,challenge,code_for(email))[0]==200
assert sql(f'''SELECT "UserId" IS NOT NULL FROM "Persons" WHERE "Id"='{person}' ''')=='t'
assert c.req('/communications/announcements')[0]==200
# Five attempts exhaust a code; expired and unknown challenges never authenticate.
d=Client(); exhausted=request(d,'member.club2@example.test'); right=code_for('member.club2@example.test')
for _ in range(5): assert verify(d,exhausted,'000000')[0]==401
assert verify(d,exhausted,right)[0]==401
unknown=request(d,'unknown-'+uuid.uuid4().hex+'@example.test'); assert verify(d,unknown,'123456')[0]==401
expired=request(d,'member.zone2@example.test'); right=code_for('member.zone2@example.test')
sql(f'''UPDATE "LoginChallenges" SET "ExpiresAt"=now()-interval '1 minute' WHERE "Id"='{expired}' ''')
assert verify(d,expired,right)[0]==401
# Resending invalidates the previous code; parallel verification consumes a code once.
e=Client(); old=request(e,'member.district2@example.test'); oldcode=code_for('member.district2@example.test')
sql(f'''UPDATE "LoginChallenges" SET "CreatedAt"=now()-interval '61 seconds' WHERE "Id"='{old}' ''')
before=len(mailbox); new=request(e,'member.district2@example.test')
for _ in range(100):
 if len(mailbox)>before:break
 time.sleep(.1)
newcode=code_for('member.district2@example.test')
assert verify(e,old,oldcode)[0]==401
from concurrent.futures import ThreadPoolExecutor
with ThreadPoolExecutor(2) as pool:
 statuses=sorted(pool.map(lambda _: verify(Client(),new,newcode)[0],range(2)))
assert statuses==[200,401],statuses
server.shutdown()
print(json.dumps(dict(emailDelivery='passed',phoneIdentifier='passed',provisioning='passed',memberPasswordBlocked='passed',staffPassword='passed',replay='passed',throttle='passed',attemptLimit='passed',expiry='passed',unknown='passed',resendInvalidation='passed',concurrentVerification='passed'),indent=2))
