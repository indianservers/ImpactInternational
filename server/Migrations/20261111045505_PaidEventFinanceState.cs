using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class PaidEventFinanceState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql("""


CREATE OR REPLACE FUNCTION clubs_event_registration_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ev "Events"; used integer;
BEGIN
 IF TG_OP='UPDATE' AND NEW."EventId"=OLD."EventId" AND NEW."TenantId"=OLD."TenantId" AND NEW."Id"=OLD."Id" AND NEW."Status"=OLD."Status" AND NEW."Companions"=OLD."Companions" THEN RETURN NEW; END IF;
 SELECT * INTO ev FROM "Events" WHERE "Id"=NEW."EventId" AND "TenantId"=NEW."TenantId" FOR NO KEY UPDATE;
 IF NEW."Status" IN ('Confirmed','Offered') THEN
  IF ev."Lifecycle" IN ('Cancelled','Archived','Completed') OR ev."Publication"<>'Published' THEN RAISE EXCEPTION 'Event cannot confirm registration' USING ERRCODE='23514'; END IF;
  IF ev."Paid" AND NOT EXISTS(SELECT 1 FROM "FinanceInvoices" i WHERE i."RegistrationId"=NEW."Id" AND i."TenantId"=NEW."TenantId" AND i."State"='Issued' AND i."Total">0 AND i."Paid"+i."Credited">=i."Total") THEN RAISE EXCEPTION 'Paid event requires verified allocation' USING ERRCODE='23514';END IF;
  SELECT COALESCE(sum(1+"Companions"),0) INTO used FROM "EventRegistrations" WHERE "EventId"=NEW."EventId" AND "Id"<>NEW."Id" AND "Status" IN ('Confirmed','Offered');
  IF used+1+NEW."Companions">ev."Capacity" THEN RAISE EXCEPTION 'Event capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF; RETURN NEW;
END $$;

ALTER TABLE "EventRegistrations" DROP CONSTRAINT "CK_Event_Registration";
ALTER TABLE "EventRegistrations" ADD CONSTRAINT "CK_Event_Registration" CHECK("Companions">=0 AND "Status" IN ('Pending','Pending Payment','Awaiting Approval','Confirmed','Waitlisted','Offered','Cancelled','Rejected'));
CREATE OR REPLACE FUNCTION finance_paid_registration() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."Status" IN ('Confirmed','Offered') AND EXISTS(SELECT 1 FROM "Events" e WHERE e."Id"=NEW."EventId" AND e."Paid") AND NOT EXISTS(SELECT 1 FROM "FinanceInvoices" i WHERE i."RegistrationId"=NEW."Id" AND i."State"='Issued' AND i."Total">0 AND i."Paid"+i."Credited">=i."Total") THEN RAISE EXCEPTION 'Paid registration requires verified allocation or authorized credit'; END IF;RETURN NULL;END $$;
CREATE CONSTRAINT TRIGGER finance_paid_registration AFTER INSERT OR UPDATE ON "EventRegistrations" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION finance_paid_registration();

""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""

CREATE OR REPLACE FUNCTION clubs_event_registration_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ev "Events"; used integer;
BEGIN
 IF TG_OP='UPDATE' AND NEW."EventId"=OLD."EventId" AND NEW."TenantId"=OLD."TenantId" AND NEW."Id"=OLD."Id" AND NEW."Status"=OLD."Status" AND NEW."Companions"=OLD."Companions" THEN RETURN NEW; END IF;
 SELECT * INTO ev FROM "Events" WHERE "Id"=NEW."EventId" AND "TenantId"=NEW."TenantId" FOR NO KEY UPDATE;
 IF NEW."Status" IN ('Confirmed','Offered') THEN
  IF ev."Lifecycle" IN ('Cancelled','Archived','Completed') OR ev."Publication"<>'Published' OR ev."Paid" THEN RAISE EXCEPTION 'Event cannot confirm registration' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(sum(1+"Companions"),0) INTO used FROM "EventRegistrations" WHERE "EventId"=NEW."EventId" AND "Id"<>NEW."Id" AND "Status" IN ('Confirmed','Offered');
  IF used+1+NEW."Companions">ev."Capacity" THEN RAISE EXCEPTION 'Event capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF; RETURN NEW;
END $$;

DROP TRIGGER IF EXISTS finance_paid_registration ON "EventRegistrations";
DROP FUNCTION IF EXISTS finance_paid_registration();
ALTER TABLE "EventRegistrations" DROP CONSTRAINT "CK_Event_Registration";
ALTER TABLE "EventRegistrations" ADD CONSTRAINT "CK_Event_Registration" CHECK("Companions">=0 AND "Status" IN ('Pending','Awaiting Approval','Confirmed','Waitlisted','Offered','Cancelled','Rejected'));
""");

        }
    }
}
