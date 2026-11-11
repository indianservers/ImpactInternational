using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class EventAuditAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
CREATE OR REPLACE FUNCTION clubs_event_registration_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE ev "Events"; used integer;
BEGIN
 IF TG_OP='UPDATE' AND NEW."EventId"=OLD."EventId" AND NEW."TenantId"=OLD."TenantId" AND NEW."Id"=OLD."Id" AND NEW."Status"=OLD."Status" AND NEW."Companions"=OLD."Companions" THEN RETURN NEW; END IF;
 SELECT * INTO ev FROM "Events" WHERE "Id"=NEW."EventId" AND "TenantId"=NEW."TenantId" FOR UPDATE;
 IF NEW."Status" IN ('Confirmed','Offered') THEN
  IF ev."Lifecycle" IN ('Cancelled','Archived','Completed') OR ev."Publication"<>'Published' OR ev."Paid" THEN RAISE EXCEPTION 'Event cannot confirm registration' USING ERRCODE='23514'; END IF;
  SELECT COALESCE(sum(1+"Companions"),0) INTO used FROM "EventRegistrations" WHERE "EventId"=NEW."EventId" AND "Id"<>NEW."Id" AND "Status" IN ('Confirmed','Offered');
  IF used+1+NEW."Companions">ev."Capacity" THEN RAISE EXCEPTION 'Event capacity exceeded' USING ERRCODE='23514'; END IF;
 END IF; RETURN NEW;
END $$;
CREATE FUNCTION clubs_event_registration_audit() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE owner_id uuid; previous text;
BEGIN
 IF TG_OP='UPDATE' THEN previous=OLD."Status"; IF NEW."Status"=OLD."Status" THEN RETURN NEW; END IF; END IF;
 SELECT "OrgId" INTO owner_id FROM "Events" WHERE "Id"=NEW."EventId";
 INSERT INTO "Audits"("Id","TenantId","OrgId","ActorId","Action","Resource","Changes","Outcome","At") VALUES(gen_random_uuid(),NEW."TenantId",owner_id,nullif(current_setting('clubs.actor',true),'')::uuid,'events.registration.status',NEW."EventId"::text,jsonb_build_object('registrationId',NEW."Id",'before',previous,'after',NEW."Status")::text,'Success',clock_timestamp()); RETURN NEW;
END $$;
CREATE TRIGGER event_registration_audit AFTER INSERT OR UPDATE ON "EventRegistrations" FOR EACH ROW EXECUTE FUNCTION clubs_event_registration_audit();
CREATE FUNCTION clubs_event_state_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."Approval" IS DISTINCT FROM OLD."Approval" OR NEW."Publication" IS DISTINCT FROM OLD."Publication" OR NEW."Lifecycle" IS DISTINCT FROM OLD."Lifecycle" OR NEW."RegistrationEnabled" IS DISTINCT FROM OLD."RegistrationEnabled" THEN
 INSERT INTO "Audits"("Id","TenantId","OrgId","ActorId","Action","Resource","Changes","Outcome","At") VALUES(gen_random_uuid(),NEW."TenantId",NEW."OrgId",nullif(current_setting('clubs.actor',true),'')::uuid,'events.state',NEW."Id"::text,jsonb_build_object('before',jsonb_build_object('approval',OLD."Approval",'publication',OLD."Publication",'lifecycle',OLD."Lifecycle",'registrationEnabled',OLD."RegistrationEnabled"),'after',jsonb_build_object('approval',NEW."Approval",'publication',NEW."Publication",'lifecycle',NEW."Lifecycle",'registrationEnabled',NEW."RegistrationEnabled"))::text,'Success',clock_timestamp()); END IF;RETURN NEW;END $$;
CREATE TRIGGER event_state_audit AFTER UPDATE ON "Events" FOR EACH ROW EXECUTE FUNCTION clubs_event_state_audit();
CREATE INDEX IF NOT EXISTS "IX_Events_EndsAt_Id" ON "Events"("EndsAt","Id");
"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS clubs_event_registration_audit() CASCADE; DROP FUNCTION IF EXISTS clubs_event_state_audit() CASCADE;DROP INDEX IF EXISTS \"IX_Events_EndsAt_Id\";"
            );
        }
    }
}
