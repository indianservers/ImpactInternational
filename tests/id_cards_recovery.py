import os,sys,json,time,pathlib,statistics,subprocess,shutil,urllib.request,uuid
sys.path.insert(0,'tests');from support import Client,sql
out=pathlib.Path('.infra/idcard-recovery');out.mkdir(exist_ok=True);db='clubs_idcards_recovery_'+uuid.uuid4().hex[:8];pg=pathlib.Path('.infra/pgsql/bin')
f=json.loads(pathlib.Path('test-results/idcard-fixture.json').read_text());mid=f['membershipId'];cid=sql(f'''SELECT "Id" FROM "DigitalCards" WHERE "MembershipId"='{mid}' AND "State"='Issued' AND "RevokedAt" IS NULL''')
c=Client();assert c.login('member@example.test')[0]==200;code,before,_=c.request('/my/id-card/download?id='+cid);assert code==200
subprocess.run([str(pg/'pg_dump.exe'),'-h','127.0.0.1','-p','55432','-U','clubs','-d','clubs_test','-Fc','-f',str(out/'database.dump')],check=True)
subprocess.run([str(pg/'createdb.exe'),'-h','127.0.0.1','-p','55432','-U','clubs',db],check=True)
subprocess.run([str(pg/'pg_restore.exe'),'-h','127.0.0.1','-p','55432','-U','clubs','-d',db,str(out/'database.dump')],check=True)
objects=out/'objects';objects.mkdir(exist_ok=True);keys=set()
for line in sql('''SELECT "Files" FROM "DigitalCards" WHERE "TemplateVersionId" IS NOT NULL''',db).splitlines():keys.update(v for v in json.loads(line).values() if v)
for line in sql('''SELECT "Snapshot" FROM "DigitalCards" WHERE "TemplateVersionId" IS NOT NULL''',db).splitlines():keys.update(v for v in json.loads(line).get('images',{}).values() if len(v)==32)
for line in sql('''SELECT "Layout" FROM "IdCardTemplateVersions"''',db).splitlines():keys.update(json.loads(line).get('assets',{}).values())
for k in keys:shutil.copy2(pathlib.Path('.infra/communication-objects')/k,objects/k)
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':f'Host=127.0.0.1;Port=55432;Database={db};Username=clubs','Communications__StoragePath':str(objects.resolve()),'Logging__LogLevel__Default':'Warning'}
with (out/'api.log').open('w') as log:
 p=subprocess.Popen(['dotnet',os.environ.get('CLUBS_IDCARD_ASSEMBLY','.infra/idcard-acceptance-final/Clubs.Api.dll'),'--urls','http://127.0.0.1:5089'],env=env,stdout=log,stderr=log)
 try:
  for _ in range(100):
   try:urllib.request.urlopen('http://127.0.0.1:5089/health/ready',timeout=1);break
   except:time.sleep(.2)
  import support;support.BASE='http://127.0.0.1:5089';restored=Client();assert restored.login('member@example.test')[0]==200;code,after,_=restored.request('/my/id-card/download?id='+cid);assert code==200 and before==after
  snap=json.loads(sql(f'''SELECT "Snapshot" FROM "DigitalCards" WHERE "Id"='{cid}' ''',db));token=snap['values']['card.qr_code'].split('/')[-1];assert restored.req('/public/id-cards/verify/'+token)[1]['valid']
  results={'databaseRestored':True,'privateCardObjectsCopied':len(keys),'ownerPdfIdenticalAfterRestore':True,'opaqueQrValidAfterRestore':True,'isolatedDatabase':db,'scope':'Local logical PostgreSQL restore plus card files, same host data-protection keyring; not off-host production certification'};pathlib.Path('docs/idcard-recovery.json').write_text(json.dumps(results,indent=2));print('PASS isolated card database/object recovery')
 finally:p.terminate();p.wait(timeout=15)
