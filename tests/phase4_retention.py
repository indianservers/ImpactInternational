"""Prepare old event-private fixtures, restart API, then verify default retention cleanup."""
from support import sql
import pathlib,uuid,json,sys
path=pathlib.Path('test-results/phase4-retention-fixture.json')
if '--prepare' in sys.argv:
 ids={k:str(uuid.uuid4()) for k in ['event','registration','invitation','document']}
 sql(f'''BEGIN;
 INSERT INTO "Events" SELECT(jsonb_populate_record(NULL::"Events",to_jsonb(e)||jsonb_build_object('Id','{ids['event']}','Title','Expired event-private fixture','StartsAt',now()-interval '402 days','EndsAt',now()-interval '400 days','RegistrationStartsAt',now()-interval '403 days','RegistrationEndsAt',now()-interval '401 days','RsvpDeadline',now()-interval '401 days','Approval','Draft','Publication','Draft','Lifecycle','Archived','ApprovalRequestId',NULL,'ReminderAt',NULL))).* FROM "Events" e WHERE e."OrgId"=(SELECT "Id" FROM "Orgs" WHERE "Code"='GC-006') LIMIT 1;
 INSERT INTO "EventRegistrations" SELECT(jsonb_populate_record(NULL::"EventRegistrations",to_jsonb(r)||jsonb_build_object('Id','{ids['registration']}','EventId','{ids['event']}','InvitationId',NULL,'ApprovalRequestId',NULL,'ParticipantKey','retention:{ids['registration']}','Email','retention-guest@example.test','Responses','{{"private":"fixture"}}','Accessibility','Private fixture','Dietary','Private fixture','Emergency','Private fixture','GuestNames','["Private companion"]','Status','Pending'))).* FROM "EventRegistrations" r WHERE r."EventId" IN(SELECT "Id" FROM "Events" WHERE "OrgId"=(SELECT "Id" FROM "Orgs" WHERE "Code"='GC-006')) LIMIT 1;
 INSERT INTO "EventInvitations" SELECT(jsonb_populate_record(NULL::"EventInvitations",to_jsonb(i)||jsonb_build_object('Id','{ids['invitation']}','EventId','{ids['event']}','RecipientKey','retention:{ids['invitation']}','PersonId',NULL,'Email','expired@example.test','TokenHash','{ids['invitation'].replace('-','')*2}','ProtectedToken','expired-test-only-token','Status','Cancelled','ExpiresAt',now()-interval '399 days','MailId',NULL))).* FROM "EventInvitations" i LIMIT 1;
 INSERT INTO "GovernanceDocuments" SELECT(jsonb_populate_record(NULL::"GovernanceDocuments",to_jsonb(d)||jsonb_build_object('Id','{ids['document']}','EventId','{ids['event']}','RetainUntil',now()-interval '1 day'))).* FROM "GovernanceDocuments" d WHERE d."EventId" IS NOT NULL LIMIT 1;COMMIT;''')
 path.write_text(json.dumps(ids));print('Prepared expired fictional event-private fixtures; restart test API.')
else:
 ids=json.loads(path.read_text());checks={
 'private registration fields redacted':sql(f'''SELECT "Email"='' AND "Responses"='{{}}' AND "Accessibility"='' AND "Dietary"='' AND "Emergency"='' AND "GuestNames"='[]' FROM "EventRegistrations" WHERE "Id"='{ids['registration']}' ''')=='t',
 'expired invitation email and protected secret cleared':sql(f'''SELECT "Email"='' AND "ProtectedToken"='' FROM "EventInvitations" WHERE "Id"='{ids['invitation']}' ''')=='t',
 'shared event document bytes expire':sql(f'''SELECT "Status"='Expired' AND octet_length("Content")=0 FROM "GovernanceDocuments" WHERE "Id"='{ids['document']}' ''')=='t',
 'registration provenance remains':sql(f'''SELECT "EventId"='{ids['event']}'::uuid AND "PersonId" IS NOT NULL FROM "EventRegistrations" WHERE "Id"='{ids['registration']}' ''')=='t'}
 for name,ok in checks.items():print(('PASS ' if ok else 'FAIL ')+name)
 pathlib.Path('test-results/phase4-retention.json').write_text(json.dumps(checks,indent=2));assert all(checks.values()),checks
