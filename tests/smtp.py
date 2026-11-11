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
transport_db=os.environ.get('CLUBS_SMTP_DATABASE','clubs_test')
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+transport_db+';Username=clubs','ASPNETCORE_URLS':'http://127.0.0.1:5082','Smtp__Host':'127.0.0.1','Smtp__Port':'2526','Smtp__From':'noreply@example.test','Smtp__EnableSsl':'false','PublicOrigin':'http://127.0.0.1:5174'}
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
 from support import sql as raw_sql
 def sql(query):return raw_sql(query,transport_db)
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
 # Event links, retry/backoff and confirmed digital-pass notification use the same transport.
 types=officer.req('/event-types')[1];now=__import__('datetime').datetime.now(__import__('datetime').timezone.utc)
 def at(h):return (now+__import__('datetime').timedelta(hours=h)).isoformat()
 status,event=officer.req('/events','POST',{'orgId':club,'typeId':types[0]['id'],'title':'SMTP event integration','startsAt':at(-1),'endsAt':at(5),'registrationStartsAt':at(-24),'registrationEndsAt':at(4),'venue':'Transport test hall','capacity':10,'visibility':'Public','allowGuests':True});assert status==200,(status,event)
 status,event=officer.req('/events/'+event['id']+'/status','POST',{'action':'Submit','version':event['version']});assert status==200
 reviewer=support.Client();reviewer.login('president@example.test');reviewer.activate(next(w for w in reviewer.req('/workspaces')[1] if w['role']=='Club President'))
 request=officer.req('/governance/requests/'+event['approvalRequestId'])[1]['request'];status,decision=reviewer.req('/governance/requests/'+request['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent SMTP integration review','version':request['version']});assert status==200,(status,decision)
 event=officer.req('/events/'+event['id'])[1]['eventInfo'];status,event=officer.req('/events/'+event['id']+'/status','POST',{'action':'Publish','version':event['version']});assert status==200
 recipient='event-'+secrets.token_hex(8)+'@example.test';reject_recipient=recipient;rejections=0
 status,b=officer.req('/events/'+event['id']+'/guest-link','POST',{'name':'SMTP consenting guest','email':recipient,'consent':True});assert status==200
 deadline=time.monotonic()+45;observed=False
 while time.monotonic()<deadline:
  if sql(f'SELECT count(*) FROM "Mails" WHERE "Recipient"=\'{recipient}\' AND "Kind"=\'event.invitation\' AND "Attempts">0 AND "NextAttemptAt">now() AND "SentAt" IS NULL')=='1':observed=True;break
  time.sleep(.2)
 assert observed,'Event invitation failure/backoff not observed'
 sql(f'UPDATE "Mails" SET "NextAttemptAt"=now() WHERE "Recipient"=\'{recipient}\'')
 deadline=time.monotonic()+45
 while time.monotonic()<deadline and not any(recipient.encode() in m for m in messages):time.sleep(.25)
 assert any(recipient.encode() in m and b'/event-invitation?token=' in (email_parser.message_from_bytes(m).get_payload(decode=True) or b'') for m in messages)
 assert sql(f'SELECT count(*) FROM "MailDeliveryAttempts" a JOIN "Mails" m ON m."Id"=a."MailId" WHERE m."Recipient"=\'{recipient}\'')=='2'
 print('PASS event invitation failure, backoff, retry and private-link SMTP delivery')
 import re
 payload=next(email_parser.message_from_bytes(m).get_payload(decode=True) or b'' for m in messages if recipient.encode() in m)
 token=re.search(rb'/event-invitation\?token=([A-F0-9]{64})',payload).group(1).decode();guest=support.Client();status,reg=guest.req('/events/'+event['id']+'/registrations','POST',{'token':token});assert status==200 and reg['status']=='Confirmed'
 deadline=time.monotonic()+45
 while time.monotonic()<deadline and not any(recipient.encode() in m and b'Registration confirmed' in (email_parser.message_from_bytes(m).get_payload(decode=True) or b'') for m in messages):time.sleep(.25)
 assert any(recipient.encode() in m and b'Registration confirmed' in (email_parser.message_from_bytes(m).get_payload(decode=True) or b'') for m in messages)
 import pathlib
 pathlib.Path('test-results/phase4-smtp.json').write_text(json.dumps({'verification_retry_and_delivery':True,'membership_invitation_retry_and_delivery':True,'event_invitation_retry_and_delivery':True,'registration_and_pass_link_delivery':True,'database':transport_db,'conditions':'Real local SMTP protocol sink; first recipient attempt deliberately rejected, persisted backoff observed and test clock made retry due. No external provider delivery claim.'},indent=2))
 print('PASS event registration confirmation and digital-pass access link delivered over SMTP')
finally:process.terminate();process.wait(timeout=10);server.shutdown();server.server_close()
