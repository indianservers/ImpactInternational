"""Start disposable certification portal QA services; Ctrl+C stops owned processes."""
import os,pathlib,subprocess,time,urllib.request
root=pathlib.Path(__file__).resolve().parent.parent
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database=clubs_certified_people_qa_20261118;Username=clubs','Authentication__MemberOtpEnabled':'false','Smtp__Host':'','Logging__LogLevel__Default':'Warning'}
subprocess.run(['dotnet',str(root/'server/bin/Release/net10.0/Clubs.Api.dll'),'--migrate'],cwd=root,env=env,check=True)
processes=[];logs=[]
try:
 for name,cmd,cwd,extra in [('api',['dotnet',str(root/'server/bin/Release/net10.0/Clubs.Api.dll'),'--urls','http://127.0.0.1:5115'],root,{}),('web',['node',str(root/'client/node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port','1255','--strictPort'],root/'client',{'CLUBS_API_TARGET':'http://127.0.0.1:5115'})]:
  log=open(root/('.infra/certified-people-'+name+'.log'),'w',encoding='utf-8');logs.append(log);processes.append(subprocess.Popen(cmd,cwd=cwd,env={**env,**extra},stdout=log,stderr=log))
 for port in [5115,1255]:
  for _ in range(100):
   try:urllib.request.urlopen('http://127.0.0.1:'+str(port)+('/health' if port==5115 else ''));break
   except:time.sleep(.2)
  else:raise RuntimeError('QA service failed: '+str(port))
 print('Certified people QA ready: API5115 / web1255',flush=True)
 while all(p.poll() is None for p in processes):time.sleep(1)
finally:
 for p in processes:p.terminate()
 for p in processes:p.wait()
 for log in logs:log.close()
