using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IdCardIssueDateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION clubs_idcard_snapshot_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF OLD."State" IN ('Pending Approval','Approved','Generated','Issued','Revoked','Replaced') AND (
 (NEW."Snapshot"::jsonb #- '{values,card.issue_date}') IS DISTINCT FROM (OLD."Snapshot"::jsonb #- '{values,card.issue_date}') OR
 ((NEW."Snapshot"::jsonb #>> '{values,card.issue_date}') IS DISTINCT FROM (OLD."Snapshot"::jsonb #>> '{values,card.issue_date}') AND NOT(OLD."State"='Approved' AND NEW."State"='Generated' AND (NEW."Snapshot"::jsonb #>> '{values,card.issue_date}')=to_char(NEW."IssuedAt" AT TIME ZONE 'UTC','YYYY-MM-DD'))) OR
 NEW."TemplateVersionId" IS DISTINCT FROM OLD."TemplateVersionId" OR NEW."MembershipId"<>OLD."MembershipId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."ExpiryAt" IS DISTINCT FROM OLD."ExpiryAt" OR NEW."TokenHash"<>OLD."TokenHash" OR NEW."ProtectedToken"<>OLD."ProtectedToken" OR NEW."Serial"<>OLD."Serial" OR NEW."ReplacesId" IS DISTINCT FROM OLD."ReplacesId") THEN RAISE EXCEPTION 'Reviewed card identity and snapshot are immutable'; END IF;
 IF OLD."State" IN ('Generated','Issued','Revoked','Replaced') AND (NEW."Files"<>OLD."Files" OR (NEW."IssuedAt" AT TIME ZONE 'UTC')::date<>(OLD."IssuedAt" AT TIME ZONE 'UTC')::date) THEN RAISE EXCEPTION 'Generated artwork and printed issue date are immutable'; END IF;
 RETURN NEW; END $$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION clubs_idcard_snapshot_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF OLD."State" IN ('Pending Approval','Approved','Generated','Issued','Revoked','Replaced') AND (NEW."Snapshot"<>OLD."Snapshot" OR NEW."TemplateVersionId" IS DISTINCT FROM OLD."TemplateVersionId" OR NEW."MembershipId"<>OLD."MembershipId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."ExpiryAt" IS DISTINCT FROM OLD."ExpiryAt" OR NEW."TokenHash"<>OLD."TokenHash" OR NEW."ProtectedToken"<>OLD."ProtectedToken" OR NEW."Serial"<>OLD."Serial" OR NEW."ReplacesId" IS DISTINCT FROM OLD."ReplacesId") THEN RAISE EXCEPTION 'Reviewed card identity and snapshot are immutable'; END IF;
 IF OLD."State" IN ('Generated','Issued','Revoked','Replaced') AND NEW."Files"<>OLD."Files" THEN RAISE EXCEPTION 'Generated card file references are immutable'; END IF;
 RETURN NEW; END $$;
""");
        }
    }
}
