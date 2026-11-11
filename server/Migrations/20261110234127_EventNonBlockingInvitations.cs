using Microsoft.EntityFrameworkCore.Migrations;
namespace Clubs.Api.Migrations;
public partial class EventNonBlockingInvitations : Migration {
protected override void Up(MigrationBuilder migrationBuilder){migrationBuilder.Sql("""
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
""");}
protected override void Down(MigrationBuilder migrationBuilder){migrationBuilder.Sql("""
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
""");}
}
