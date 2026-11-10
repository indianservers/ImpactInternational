"""Sample the real API's RSS while running 1,000- and 5,000-row import suites."""
import psutil,subprocess,os,time,json,pathlib,sys
pid=int(sys.argv[1]);process=psutil.Process(pid);results=[]
for rows in (1000,5000):
 before=process.memory_info().rss;samples=[]
 with open(f'.infra/phase2-import-{rows}.log','w') as log:
  child=subprocess.Popen([sys.executable,'-u','tests/phase2_imports.py'],env={**os.environ,'CLUBS_IMPORT_ROWS':str(rows)},stdout=log,stderr=subprocess.STDOUT)
  while child.poll() is None:
   samples.append(process.memory_info().rss);time.sleep(.2)
  assert child.returncode==0,f'Import suite failed: see .infra/phase2-import-{rows}.log'
 elapsed=json.loads(pathlib.Path('test-results/phase2-import-performance.json').read_text())
 results.append({**elapsed,'rss_before_mib':round(before/1048576,1),'rss_peak_mib':round(max(samples)/1048576,1),'rss_after_mib':round(process.memory_info().rss/1048576,1),'rss_samples':len(samples)})
report={'runs':results,'conditions':'Windows process working set (RSS), sampled every 200 ms across the complete import suite, not only final commit. Same API process for both runs. Includes .NET JIT, connection pools and other test endpoints; retained working set is not proof of a leak. Inputs and staging are bounded to 10 MB / 10,000 rows; EF write tracker clears every 200 rows. No claim of constant memory at arbitrary input sizes.'}
pathlib.Path('test-results/phase2-import-memory.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
