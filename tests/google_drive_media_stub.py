"""Loopback-only Google provider double; keeps uploaded bytes in process memory for QA."""
import base64, hashlib, json, re, uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit, parse_qs, urlencode

files, sessions, codes = {}, {}, {}
origin = 'http://127.0.0.1:1252'
class Handler(BaseHTTPRequestHandler):
 def log_message(self,*args): pass
 def send(self,status=200,data=None,headers=None):
  raw=data if isinstance(data,bytes) else json.dumps(data or {}).encode()
  self.send_response(status)
  for key,value in {'Content-Type':'application/json','Content-Length':str(len(raw)),'Access-Control-Allow-Origin':origin,'Access-Control-Expose-Headers':'Range, Location',**(headers or {})}.items():self.send_header(key,value)
  self.end_headers();self.wfile.write(raw)
 def body(self):
  if self.headers.get('Transfer-Encoding')=='chunked':
   chunks=[]
   while True:
    size=int(self.rfile.readline().strip(),16)
    if size==0:self.rfile.readline();break
    chunks.append(self.rfile.read(size));self.rfile.read(2)
   return b''.join(chunks)
  return self.rfile.read(int(self.headers.get('Content-Length',0)))
 def do_OPTIONS(self):self.send(204,b'',{'Access-Control-Allow-Methods':'GET,POST,PUT,OPTIONS','Access-Control-Allow-Headers':'Content-Type,Content-Range'})
 def do_GET(self):
  url=urlsplit(self.path);q=parse_qs(url.query);path=url.path
  if path=='/authorize':
   code=uuid.uuid4().hex;codes[code]=q
   return self.send(302,b'',{'Location':q['redirect_uri'][0]+'?'+urlencode({'code':code,'state':q['state'][0]})})
  if path=='/oauth2/v3/userinfo':
   acct=self.headers.get('Authorization','').split('-')[-1]
   return self.send(data={'sub':acct,'email':'storage-'+acct+'@example.test','email_verified':True})
  if path=='/drive/v3/files/generateIds':return self.send(data={'ids':['file-'+uuid.uuid4().hex]})
  if path.startswith('/drive/v3/files/'):
   fid=path.split('/')[-1];f=files.get(fid)
   if not f or not f.get('ready'):return self.send(404,{'error':'not uploaded'})
   if q.get('alt')==['media']:
    raw=f['bytes'];headers={'Content-Type':f['mimeType'],'Accept-Ranges':'bytes'}
    if self.headers.get('Range'):
     start,end=self.headers['Range'][6:].split('-');start=int(start or 0);end=int(end or len(raw)-1)
     headers['Content-Range']=f'bytes {start}-{end}/{len(raw)}';return self.send(206,raw[start:end+1],headers)
    return self.send(200,raw,headers)
   return self.send(data={k:v for k,v in f.items() if k not in ['bytes','ready']})
  if path=='/health':return self.send(data={'ready':True})
  return self.send(404)
 def do_POST(self):
  path=urlsplit(self.path).path;raw=self.body()
  if path=='/token':
   q=parse_qs(raw.decode());acct='primary'
   if q.get('grant_type')==['authorization_code']:
    auth=codes.pop(q['code'][0],None)
    if not auth:return self.send(400)
    challenge=base64.urlsafe_b64encode(hashlib.sha256(q['code_verifier'][0].encode()).digest()).decode().rstrip('=')
    if challenge!=auth['code_challenge'][0]:return self.send(400)
    acct=auth.get('login_hint',['primary'])[0].split('@')[0]
   elif q.get('grant_type')!=['refresh_token']:return self.send(400)
   return self.send(data={'access_token':'fake-access-'+acct,'refresh_token':'fake-refresh-'+acct,'expires_in':3600,'scope':'openid email https://www.googleapis.com/auth/drive.file'})
  if path=='/revoke':return self.send()
  if path=='/drive/v3/files':
   f=json.loads(raw);fid='folder-'+uuid.uuid4().hex;files[fid]={**f,'id':fid,'ready':True};return self.send(data={'id':fid})
  if path=='/upload/drive/v3/files':
   f=json.loads(raw);f.update(size='0',trashed=False,ready=False,bytes=b'');files[f['id']]=f
   sid=uuid.uuid4().hex;sessions[sid]=f['id'];return self.send(200,{}, {'Location':'http://127.0.0.1:5113/sessions/'+sid})
  return self.send(404)
 def do_PUT(self):
  sid=urlsplit(self.path).path.split('/')[-1];fid=sessions.get(sid);raw=self.body()
  if not fid:return self.send(404)
  f=files[fid];r=self.headers.get('Content-Range','')
  if r.startswith('bytes */'):
   if f['ready']:return self.send(data={'id':fid})
   return self.send(308,{}, {'Range':'bytes=0-'+str(len(f['bytes'])-1)} if f['bytes'] else {})
  match=re.match(r'bytes (\d+)-(\d+)/(\d+)',r)
  if not match:return self.send(400)
  start,end,total=map(int,match.groups())
  if start!=len(f['bytes']) or len(raw)!=end-start+1:return self.send(400)
  f['bytes']+=raw;f['size']=str(len(f['bytes']));f['ready']=len(f['bytes'])==total
  if f['ready']:return self.send(200,{'id':fid})
  return self.send(308,{}, {'Range':'bytes=0-'+str(end)})

if __name__=='__main__':ThreadingHTTPServer(('127.0.0.1',5113),Handler).serve_forever()
