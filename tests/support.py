"""Shared test client for JSON, authenticated uploads and binary downloads."""
import json,urllib.request,urllib.error,http.cookiejar,os,pathlib,uuid,subprocess,time
PASSWORD=os.environ.get('CLUBS_SEED_PASSWORD') or pathlib.Path('.infra/seed-password').read_text()
BASE=os.environ.get('CLUBS_TEST_URL','http://127.0.0.1:5081')
PSQL=os.environ.get('CLUBS_PSQL',str(pathlib.Path('.infra/pgsql/bin/psql.exe').resolve()))
def sql(query,database=None):
 database=database or os.environ.get('CLUBS_TEST_DATABASE','clubs_test')
 return subprocess.check_output([PSQL,'-h','127.0.0.1','-p','55432','-U','clubs','-d',database,'-X','-At','-v','ON_ERROR_STOP=1','-c',query],text=True).strip()
class Client:
 def __init__(self):
  self.jar=http.cookiejar.CookieJar();self.http=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar));self.csrf=self.req('/auth/csrf')[1]['token']
 def request(self,path,method='GET',data=None,headers=None):
  h={'X-CSRF':getattr(self,'csrf',''),**(headers or {})};r=urllib.request.Request(BASE+'/api/v1'+path,data=data,method=method,headers=h)
  for attempt in range(7):
   try:
    with self.http.open(r) as response:return response.status,response.read(),dict(response.headers)
   except urllib.error.HTTPError as e:
    if e.code==429 and os.environ.get('CLUBS_TEST_RETRY_RATE_LIMIT')=='1' and attempt<6:
     e.close();time.sleep(10);continue
    return e.code,e.read(),dict(e.headers)
 def req(self,path,method='GET',data=None):
  status,body,headers=self.request(path,method,None if data is None else json.dumps(data).encode(),{'Content-Type':'application/json'})
  try:return status,json.loads(body) if body else None
  except:return status,body
 def login(self,email):return self.req('/auth/login','POST',{'email':email,'password':PASSWORD})
 def activate(self,w):return self.req('/workspaces/'+w['id']+'/activate','POST',{})
 def upload(self,path,filename,content,fields=None,content_type='application/octet-stream'):
  boundary='----test'+uuid.uuid4().hex;parts=[]
  for key,value in (fields or {}).items():parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
  parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{filename}"\r\nContent-Type: {content_type}\r\n\r\n'.encode()+content+b'\r\n');parts.append(f'--{boundary}--\r\n'.encode());status,body,h=self.request(path,'POST',b''.join(parts),{'Content-Type':'multipart/form-data; boundary='+boundary})
  try:return status,json.loads(body)
  except:return status,body

