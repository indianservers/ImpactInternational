"""Exercise the actual SMTP outbox against a local disposable SMTP protocol sink."""
from http.cookiejar import CookieJar
import socketserver,threading,subprocess,os,time,urllib.request,http.cookiejar,json,secrets
messages=[]
class SMTP(socketserver.StreamRequestHandler):
 def handle(self):
  self.wfile.write(b'220 localhost SMTP test sink\r\n');self.wfile.flush()
  while True:
   line=self.rfile.readline()
   if not line:return
   command=line.split(b' ')[0].strip().upper()
   if command in (b'EHLO',b'HELO'):reply=b'250-localhost\r\n250 SIZE 100000\r\n'
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
 data={'name':'SMTP Integration','email':email,'phone':'+910000000000','password':password,'confirmation':password,'agreed':True}
 req=urllib.request.Request('http://127.0.0.1:5082/api/v1/auth/register',data=json.dumps(data).encode(),headers={'Content-Type':'application/json','X-CSRF':csrf},method='POST');assert client.open(req).status==202
 deadline=time.monotonic()+45
 while time.monotonic()<deadline and not any(email.encode() in m for m in messages):time.sleep(.25)
 assert any(email.encode() in m and b'/complete?token=' in m for m in messages),'SMTP delivery not received'
 print('PASS PostgreSQL outbox delivered a verification message over real local SMTP')
finally:process.terminate();process.wait(timeout=10);server.shutdown();server.server_close()
