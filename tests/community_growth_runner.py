"""Start the isolated activity-media QA stack. Ctrl+C stops all owned processes."""
import os,pathlib,subprocess,time,urllib.request
root=pathlib.Path(__file__).resolve().parent.parent
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database=clubs_community_growth_qa_20261118;Username=clubs',
 'GoogleMedia__ClientId':'local-test-client','GoogleMedia__ClientSecret':'local-test-only','GoogleMedia__RedirectUri':'http://127.0.0.1:1252/api/v1/google-media/callback','GoogleMedia__TestBase':'http://127.0.0.1:5113',
 'Authentication__MemberOtpEnabled':'false','Smtp__Host':'','Logging__LogLevel__Default':'Warning'}
logs=[];processes=[]
try:
 for name,cmd,cwd,extra in [
  ('provider',['python','tests/google_drive_media_stub.py'],root,{}),
  ('api',['dotnet',str(root/'server/bin/Release/net10.0/Clubs.Api.dll'),'--urls','http://127.0.0.1:5112'],root,{}),
  ('web',['node',str(root/'client/node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port','1252','--strictPort'],root/'client',{'CLUBS_API_TARGET':'http://127.0.0.1:5112'})]:
  log=open(root/('.infra/community-growth-'+name+'.log'),'w',encoding='utf-8');logs.append(log)
  processes.append(subprocess.Popen(cmd,cwd=cwd,env={**env,**extra},stdout=log,stderr=log))
 for port in [5113,5112,1252]:
  for _ in range(100):
   try:urllib.request.urlopen('http://127.0.0.1:'+str(port)+('/health' if port!=1252 else ''));break
   except:time.sleep(.2)
  else:raise RuntimeError('QA service unavailable: '+str(port))
 print('Community growth QA ready: API5112 / web1252 / fake Google5113',flush=True)
 while all(p.poll() is None for p in processes):time.sleep(1)
finally:
 for p in processes:
  p.terminate()
 for log in logs:log.close()
