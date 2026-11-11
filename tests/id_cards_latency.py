import os,sys,json,time,pathlib,statistics
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5088';sys.path.insert(0,'tests');from support import Client,sql
c=Client();assert c.login('admin@example.test')[0]==200;w=next(x for x in c.req('/workspaces')[1] if x['role']=='Association Super Admin' and x['association'].startswith('Global'));c.activate(w)
after=None;seen=set();times=[]
for n in range(30):
 t=time.perf_counter();code,data=c.req('/id-cards/members'+('?after='+after if after else ''));times.append((time.perf_counter()-t)*1000);assert code==200,(code,data)
 rows=data['items'];assert len(rows)<=50 and not any(r['id'] in seen for r in rows);seen.update(r['id'] for r in rows);after=data['next'];assert after
snap=json.loads(sql('''SELECT "Snapshot" FROM "DigitalCards" WHERE "State"='Issued' AND "Snapshot"<>'{}' LIMIT 1''','clubs_idcards_scale'));token=snap['values']['card.qr_code'].split('/')[-1];verify=[]
for n in range(20):
 t=time.perf_counter();code,data=c.req('/public/id-cards/verify/'+token);verify.append((time.perf_counter()-t)*1000);assert code==200 and data['valid']
def metric(a):return {'samples':len(a),'medianMs':round(statistics.median(a),2),'p95Ms':round(sorted(a)[int(len(a)*.95)-1],2),'maxMs':round(max(a),2)}
r={'conditions':'Real PostgreSQL synthetic scale fixture, 101035 people, loopback warm sequential requests, 50-row keyset pages; development workstation','keysetPages':metric(times),'uniqueMemberships':len(seen),'publicVerification':metric(verify)};pathlib.Path('docs/idcard-latency.json').write_text(json.dumps(r,indent=2));print(json.dumps(r))
