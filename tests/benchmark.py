"""Run EXPLAIN ANALYZE on a rollback-only 10,000-club fixture in clubs_test."""
import pathlib,subprocess,json,re,statistics,os
psql=os.environ.get('CLUBS_PSQL',str(pathlib.Path('.infra/pgsql/bin/psql.exe').resolve()))
query='''WITH RECURSIVE tree AS (SELECT * FROM "Orgs" WHERE "TenantId"=(SELECT "TenantId" FROM "Orgs" WHERE "Code"='GC-001') AND "Code"='GC-001' UNION ALL SELECT o.* FROM "Orgs" o JOIN tree t ON o."ParentId"=t."Id" AND o."TenantId"=t."TenantId") SELECT "Id","Name","Code" FROM tree WHERE "Status"='Active' ORDER BY "Name","Id" LIMIT 20'''
fixture='''BEGIN;
SET LOCAL statement_timeout='120s';
INSERT INTO "Orgs" ("Id","TenantId","LevelId","ParentId","Name","Code","ShortName","Country","State","City","Address","Email","Phone","Website","Logo","TimeZone","Currency","Status","Version","CreatedAt","UpdatedAt") SELECT gen_random_uuid(),o."TenantId",o."LevelId",o."ParentId",'Benchmark Club '||lpad(n::text,5,'0'),'BENCH-'||n,'','India','','Test City','','','','','','UTC','INR','Active',1,now(),now() FROM "Orgs" o CROSS JOIN generate_series(1,10000) n WHERE o."Code"='GC-006';
ANALYZE "Orgs";
SET LOCAL statement_timeout='15s';
'''
script=fixture+';\n'.join('EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) '+query for _ in range(31))+';\nROLLBACK;'
result=subprocess.run([psql,'-h','127.0.0.1','-p','55432','-U','clubs','-d','clubs_test','-X','-At','-v','ON_ERROR_STOP=1'],input=script,text=True,capture_output=True)
if result.returncode: raise RuntimeError(result.stderr)
values=[float(x) for x in re.findall(r'"Execution Time": ([\d.]+)',result.stdout)][1:]
pathlib.Path('test-results').mkdir(exist_ok=True);pathlib.Path('test-results/explain.txt').write_text(result.stdout)
report={'fixture_clubs':10000,'samples':len(values),'median_database_ms':round(statistics.median(values),3),'p95_database_ms':round(sorted(values)[int(len(values)*.95)-1],3),'conditions':'PostgreSQL 17.6, Windows local, rollback-only fictional fixture, warm repeated EXPLAIN ANALYZE BUFFERS of scoped recursive hierarchy projection sorted to 20 rows; no concurrent users. First sample discarded. Full plans in explain.txt.'}
pathlib.Path('test-results/database.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
