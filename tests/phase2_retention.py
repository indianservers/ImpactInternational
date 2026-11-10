"""Prepare expired fictional fixtures, restart test API, then verify startup cleanup."""
from support import sql
import pathlib,json,sys,uuid,time
path=pathlib.Path('test-results/phase2-retention-fixture.json')
if '--prepare' in sys.argv:
 ids={k:str(uuid.uuid4()) for k in ['document','mail','verification']}
 ids['job']=sql('SELECT "Id" FROM "MemberImportJobs" WHERE "Status"=\'Completed\' AND "ImportedRows">0 ORDER BY "CreatedAt" DESC LIMIT 1')
 sql(f'''INSERT INTO "MembershipDocuments" SELECT (jsonb_populate_record(NULL::"MembershipDocuments",to_jsonb(d)||jsonb_build_object('Id','{ids['document']}','RetainUntil',now()-interval '1 day'))).* FROM "MembershipDocuments" d LIMIT 1;
 INSERT INTO "Mails" SELECT (jsonb_populate_record(NULL::"Mails",to_jsonb(m)||jsonb_build_object('Id','{ids['mail']}','TokenHash','{ids['mail'].replace('-','')*2}','Body','retention test content','Recipient','expired@example.test','Link','/expired','ExpiresAt',now()-interval '1 day'))).* FROM "Mails" m LIMIT 1;
 INSERT INTO "DigitalCardVerifications" ("Id","CardId","Valid","At") VALUES ('{ids['verification']}',NULL,false,now()-interval '91 days');
 UPDATE "MemberImportJobs" SET "CreatedAt"=now()-interval '31 days' WHERE "Id"='{ids['job']}';''')
 path.write_text(json.dumps(ids));print('Prepared expired fictional fixtures; restart clubs_test API before verification.')
else:
 ids=json.loads(path.read_text());checks={}
 checks['expired document bytes cleared']=sql(f'SELECT "Status"=\'Expired\' AND octet_length("Content")=0 FROM "MembershipDocuments" WHERE "Id"=\'{ids["document"]}\'')=='t'
 checks['expired notification contents cleared']=sql(f'SELECT "Body"=\'\' AND "Recipient"=\'\' AND "Link"=\'\' FROM "Mails" WHERE "Id"=\'{ids["mail"]}\'')=='t'
 checks['old import source text redacted']=sql(f'SELECT count(*) FROM "MemberImportRows" WHERE "JobId"=\'{ids["job"]}\' AND ("Data"<>\'{{}}\' OR "Candidates"<>\'[]\')')=='0'
 checks['import provenance remains valid']=int(sql(f'SELECT count(*) FROM "Memberships" m JOIN "MemberImportRows" r ON r."Id"=m."ImportRowId" WHERE r."JobId"=\'{ids["job"]}\''))>0
 checks['old verification events removed']=sql(f'SELECT count(*) FROM "DigitalCardVerifications" WHERE "Id"=\'{ids["verification"]}\'')=='0'
 pathlib.Path('test-results/phase2-retention.json').write_text(json.dumps(checks,indent=2))
 for name,passed in checks.items():print(('PASS ' if passed else 'FAIL ')+name)
 assert all(checks.values())
