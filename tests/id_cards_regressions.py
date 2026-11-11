"""Selected Phase 1–8 regression suites on an isolated pre-card fixture clone."""
import sys,os,pathlib,subprocess,time,urllib.request,json,re
suites=['integration','phase2','phase2_extended','phase2_imports','phase3','phase3_extended','phase3_security','phase4','phase4_extended','phase4_security','phase5','phase5_extended','phase5_security','phase6','phase6_extended','phase6_edges','phase6_scope','phase6_workflows','phase7','phase7_extended','phase7_security','phase7_sources','phase8']
folder=pathlib.Path('.infra/idcard-regressions');folder.mkdir(exist_ok=True)
assembly=os.environ.get('CLUBS_IDCARD_ASSEMBLY','.infra/idcard-acceptance/Clubs.Api.dll')
database=os.environ.get('CLUBS_REGRESSION_DATABASE','clubs_idcards_acceptance_regression')
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','CLUBS_TEST_URL':'http://127.0.0.1:5087','CLUBS_TEST_DATABASE':database,'ConnectionStrings__Database':f'Host=127.0.0.1;Port=55432;Database={database};Username=clubs','Logging__LogLevel__Default':'Warning'}
with (folder/'migration.log').open('w') as output:subprocess.run(['dotnet',assembly,'--migrate'],env=env,stdout=output,stderr=subprocess.STDOUT,check=True)
results=json.loads(pathlib.Path("docs/idcard-regressions.json").read_text())["suites"] if pathlib.Path("docs/idcard-regressions.json").exists() else []
start=sys.argv[1] if len(sys.argv)>1 else suites[0]
results=[r for r in results if r["suite"] in suites[:suites.index(start)]]
for suite in suites[suites.index(start):]:
 with (folder/(suite+'-api.log')).open('w') as apiLog,(folder/(suite+'.log')).open('w') as output:
  process=subprocess.Popen(['dotnet',assembly,'--urls','http://127.0.0.1:5087'],env=env,stdout=apiLog,stderr=apiLog)
  try:
   for _ in range(100):
    try:urllib.request.urlopen(env['CLUBS_TEST_URL']+'/health/ready',timeout=1);break
    except:time.sleep(.2)
   completed=subprocess.run(['python','-u','tests/'+suite+'.py'],env=env,stdout=output,stderr=subprocess.STDOUT,timeout=600)
   assert completed.returncode==0,(suite,str(folder/(suite+'.log')))
   paths=re.findall(r"['\"](test-results/[^'\"]+\.json)['\"]",pathlib.Path('tests/'+suite+'.py').read_text());file=next((pathlib.Path(p) for p in paths if 'fixture' not in p and pathlib.Path(p).exists()),None)
   data=json.loads(file.read_text()) if file else [];checks=data if isinstance(data,list) else data.get('tests',data.get('checks',[]));results.append({'suite':suite,'assertions':len(checks),'passed':all(x.get('passed',False) for x in checks)});pathlib.Path('docs/idcard-regressions.json').write_text(json.dumps({'suites':results,'assertions':sum(x['assertions'] for x in results),'allPassed':all(x['passed'] for x in results)},indent=2));print('PASS',suite,flush=True)
  finally:process.terminate();process.wait(timeout=15)
print('PASS all selected Phase 1–8 regression suites',flush=True)
