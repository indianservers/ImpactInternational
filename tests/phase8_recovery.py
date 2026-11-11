"""Isolated PostgreSQL backup/restore drill. Never restores over the source database."""
import pathlib,subprocess,uuid,json,time,hashlib,shutil,os
root=pathlib.Path('.infra/phase8-recovery');root.mkdir(exist_ok=True)
bin=pathlib.Path('.infra/pgsql/bin');target='clubs_phase8_restore_'+uuid.uuid4().hex[:8];args=['-h','127.0.0.1','-p','55432','-U','clubs'];dump=root/(target+'.dump');started=time.monotonic()
def run(name,*a):return subprocess.check_output([str(bin/(name+'.exe')),*args,*a],text=True,stderr=subprocess.STDOUT).strip()
def sql(db,q):return run('psql','-d',db,'-X','-At','-v','ON_ERROR_STOP=1','-c',q)
subprocess.run([str(bin/'pg_dump.exe'),*args,'-Fc','-d','clubs_test','-f',str(dump)],check=True)
run('createdb',target);run('pg_restore','--exit-on-error','--no-owner','-d',target,str(dump))
tables=['Persons','Memberships','FinanceInvoices','FinanceJournals','KnowledgePassages','CommunicationExecutions'];counts={t:{'source':sql('clubs_test',f'SELECT count(*) FROM "{t}"'),'restored':sql(target,f'SELECT count(*) FROM "{t}"')} for t in tables};assert all(v['source']==v['restored'] for v in counts.values())
# Restore data checked before rollback: this database is disposable, source remains intact.
env=__import__('os').environ.copy();env['ASPNETCORE_ENVIRONMENT']='Development';env['ConnectionStrings__Database']=f'Host=127.0.0.1;Port=55432;Database={target};Username=clubs'
for migration in ['PaidEventFinanceState','IntelligenceFoundation']:
 log=root/(migration+'.log')
 with log.open('w') as output:subprocess.run(['.tools/dotnet-ef.exe','database','update',migration,'--project','server','--no-build'],env=env,stdout=output,stderr=subprocess.STDOUT,check=True)
assert sql(target,'SELECT count(*) FROM "__EFMigrationsHistory"')=='34'
fresh='clubs_phase8_fresh_'+uuid.uuid4().hex[:8];run('createdb',fresh);env['ConnectionStrings__Database']=f'Host=127.0.0.1;Port=55432;Database={fresh};Username=clubs'
with (root/'fresh.log').open('w') as output:subprocess.run(['.tools/dotnet-ef.exe','database','update','--project','server','--no-build'],env=env,stdout=output,stderr=subprocess.STDOUT,check=True)
assert sql(fresh,'SELECT count(*) FROM "__EFMigrationsHistory"')=='34'
bundle=root/target;bundle.mkdir(exist_ok=True)
objectSource=pathlib.Path(os.environ.get('Communications__StoragePath','.infra/communication-objects')).resolve();objects=bundle/'objects';shutil.copytree(objectSource,objects)
objectFiles=[p for p in objectSource.iterdir() if p.is_file()];assert all(hashlib.sha256(p.read_bytes()).digest()==hashlib.sha256((objects/p.name).read_bytes()).digest() for p in objectFiles)
keySource=pathlib.Path(os.environ.get('DataProtection__Path',str(pathlib.Path(os.environ['LOCALAPPDATA'])/'ASP.NET'/'DataProtection-Keys'))).resolve();keys=bundle/'keys';shutil.copytree(keySource,keys)
cipher=sql(target,'SELECT "ProtectedDetails" FROM "FinanceBanks" LIMIT 1');assert cipher;cipherPath=bundle/'protected-bank.txt';cipherPath.write_text(cipher)
with (bundle/'key-recovery.log').open('w') as output:subprocess.run(['dotnet','run','--project','tests/RecoveryChecks','--',str(keys.resolve()),str(cipherPath.resolve())],stdout=output,stderr=subprocess.STDOUT,check=True)
report={'freshDatabase':fresh,'freshMigrations':34,'privateObjectsCopiedAndHashVerified':len(objectFiles),'copiedKeyRingDecryptsRestoredBank':True,'database':target,'dumpSha256':hashlib.sha256(dump.read_bytes()).hexdigest(),'bytes':dump.stat().st_size,'counts':counts,'migrationsAfterReapply':34,'phase8RollbackReapply':True,'seconds':round(time.monotonic()-started,2),'limitations':['Same host database drill only','Copied objects and keys checked locally; off-host recovery and OS-bound key portability remain unverified','No production RPO/RTO claim']};pathlib.Path('docs/phase8-recovery.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
