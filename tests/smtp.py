"""Exercise the actual SMTP outbox against a local disposable SMTP protocol sink."""
from http.cookiejar import CookieJar
import email as email_parser
import socketserver,threading,subprocess,os,time,urllib.request,http.cookiejar,json,secrets
messages=[]
reject_recipient=""
rejections=0
class SMTP(socketserver.StreamRequestHandler):
 def handle(self):
  global rejections
  self.wfile.write(b'220 localhost SMTP test sink\r\n');self.wfile.flush()
  while True:
   line=self.rfile.readline()
   if not line:return
   command=line.split(b' ')[0].strip().upper()
   if command in (b'EHLO',b'HELO'):reply=b'250-localhost\r\n250 SIZE 100000\r\n'
   elif command==b'RCPT' and reject_recipient.encode() in line and rejections==0:
    rejections+=1;reply=b'451 Temporary failure for retry test\r\n'
   elif command==b'DATA':
    self.wfile.write(b'354 End with dot\r\n');self.wfile.flush();data=[]
    while True:
     row=self.rfile.readline()
     if row in (b'.\r\n',b''):break
     data.append(row)
    messages.append(b''.join(data));reply=b'250 queued\r\n'
   elif command==b'QUIT':self.wfile.write(b'221 bye\r\n');self.wfile.flush();return
   else:reply=b'250 OK\r\n'
   self.wfile.write(reply);self.wfile.flush()
server=socketserver.ThreadingTCPServer(('127.0.0.1',2526),SMTP);threading.Thread(target=server.serve_forever,daemon=True).start()
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database=clubs_test;Username=clubs','ASPNETCORE_URLS':'http://127.0.0.1:5082','Smtp__Host':'127.0.0.1','Smtp__Port':'2526','Smtp__From':'noreply@example.test','Smtp__EnableSsl':'false','PublicOrigin':'http://127.0.0.1:5174'}
process=subprocess.Popen(['dotnet','server/bin/Debug/net10.0/Clubs.Api.dll'],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
try:
 for _ in range(50):
  try:urllib.request.urlopen('http://127.0.0.1:5082/health');break
  except Exception:time.sleep(.2)
 client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(CookieJar()))
 csrf=json.load(client.open('http://127.0.0.1:5082/api/v1/auth/csrf'))['token']
 password=secrets.token_hex(20);email='smtp-'+secrets.token_hex(8)+'@example.test'
 reject_recipient=email
 data={'name':'SMTP Integration','email':email,'phone':'+910000000000','password':password,'confirmation':password,'agreed':True}
 req=urllib.request.Request('http://127.0.0.1:5082/api/v1/auth/register',data=json.dumps(data).encode(),headers={'Content-Type':'application/json','X-CSRF':csrf},method='POST');assert client.open(req).status==202
 from support import sql
 deadline=time.monotonic()+45
 observed=False
 while time.monotonic()<deadline:
  row=sql(f"SELECT count(*) FROM \"Mails\" m JOIN \"Users\" u ON u.\"Id\"=m.\"UserId\" WHERE u.\"Email\"='{email}' AND m.\"Attempts\">0 AND m.\"NextAttemptAt\">now() AND m.\"SentAt\" IS NULL")
  if row=='1':observed=True;break
  time.sleep(.2)
 assert observed,'SMTP failure was not persisted with future backoff'
 sql(f"UPDATE \"Mails\" SET \"NextAttemptAt\"=now() WHERE \"UserId\"=(SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='{email}')")
 print('PASS temporary SMTP failure persisted with backoff; retry made due by test clock')
 deadline=time.monotonic()+45
 while time.monotonic()<deadline and not any(email.encode() in m for m in messages):time.sleep(.25)
 assert any(email.encode() in m and b'/complete?token=' in (email_parser.message_from_bytes(m).get_payload(decode=True) or b'') for m in messages),'SMTP delivery not received'
 assert sql(f"SELECT count(*) FROM \"MailDeliveryAttempts\" a JOIN \"Mails\" m ON m.\"Id\"=a.\"MailId\" JOIN \"Users\" u ON u.\"Id\"=m.\"UserId\" WHERE u.\"Email\"='{email}'")=='2'
 print('PASS failed and successful delivery attempts both recorded')
 print('PASS PostgreSQL outbox delivered a verification message over real local SMTP')
 # Exercise the membership notification body/recipient path, not only security links.
 import support
 support.BASE='http://127.0.0.1:5082'
 officer=support.Client();assert officer.login('admin@example.test')[0]==200
 workspace=next(w for w in officer.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));officer.activate(workspace)
 config=officer.req('/membership/config')[1];category=next(c for c in config['categories'] if c['code']=='REG')
 club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'')
 recipient='notification-'+secrets.token_hex(8)+'@example.test';reject_recipient=recipient;rejections=0
 status,invitation=officer.req('/membership/invitations','POST',{'clubId':club,'categoryId':category['id'],'email':recipient});assert status==200,(status,invitation)
 deadline=time.monotonic()+45;observed=False
 while time.monotonic()<deadline:
  if sql(f'SELECT count(*) FROM "Mails" WHERE "Recipient"=\'{recipient}\' AND "Attempts">0 AND "NextAttemptAt">now() AND "SentAt" IS NULL')=='1':observed=True;break
  time.sleep(.2)
 assert observed,'Membership notification failure/backoff was not persisted'
 sql(f'UPDATE "Mails" SET "NextAttemptAt"=now() WHERE "Recipient"=\'{recipient}\'')
 deadline=time.monotonic()+45
 while time.monotonic()<deadline and not any(recipient.encode() in m for m in messages):time.sleep(.25)
 assert any(recipient.encode() in m and b'/join?token=' in (email_parser.message_from_bytes(m).get_payload(decode=True) or b'') for m in messages)
 assert sql(f'SELECT count(*) FROM "MailDeliveryAttempts" a JOIN "Mails" m ON m."Id"=a."MailId" WHERE m."Recipient"=\'{recipient}\'')=='2'
 print('PASS membership invitation notification failure, backoff, retry and actual SMTP body delivery')
finally:process.terminate();process.wait(timeout=10);server.shutdown();server.server_close()
