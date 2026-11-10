using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class HierarchyGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """

CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX "IX_Orgs_Name_Search" ON "Orgs" USING gin ("Name" gin_trgm_ops);
ALTER TABLE "Orgs" ADD CONSTRAINT "CK_Org_Identity" CHECK(length(trim("Name")) BETWEEN 2 AND 200 AND length("Code") BETWEEN 1 AND 40 AND "Status" IN ('Active','Pending','Archived') AND "Id" IS DISTINCT FROM "ParentId");
ALTER TABLE "Orgs" ADD CONSTRAINT "CK_Org_Currency" CHECK(length("Currency")=3);
CREATE FUNCTION clubs_hierarchy_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE permitted uuid; actual uuid;
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."TenantId"::text));
 IF TG_OP='UPDATE' AND (NEW."TenantId"<>OLD."TenantId" OR NEW."LevelId"<>OLD."LevelId") THEN RAISE EXCEPTION 'Tenant and level are immutable' USING ERRCODE='23514'; END IF;
 SELECT "ParentLevelId" INTO permitted FROM "Levels" WHERE "Id"=NEW."LevelId" AND "TenantId"=NEW."TenantId";
 IF NEW."ParentId" IS NULL THEN
  IF permitted IS NOT NULL THEN RAISE EXCEPTION 'Parent required' USING ERRCODE='23514'; END IF;
 ELSE
  SELECT "LevelId" INTO actual FROM "Orgs" WHERE "Id"=NEW."ParentId" AND "TenantId"=NEW."TenantId";
  IF actual IS NULL OR actual IS DISTINCT FROM permitted THEN RAISE EXCEPTION 'Invalid parent type or tenant' USING ERRCODE='23514'; END IF;
  IF EXISTS(WITH RECURSIVE ancestors AS (SELECT "Id","ParentId" FROM "Orgs" WHERE "Id"=NEW."ParentId" AND "TenantId"=NEW."TenantId" UNION SELECT o."Id",o."ParentId" FROM "Orgs" o JOIN ancestors a ON o."Id"=a."ParentId" WHERE o."TenantId"=NEW."TenantId") SELECT 1 FROM ancestors WHERE "Id"=NEW."Id") THEN RAISE EXCEPTION 'Cycle rejected' USING ERRCODE='23514'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER organization_hierarchy_guard BEFORE INSERT OR UPDATE ON "Orgs" FOR EACH ROW EXECUTE FUNCTION clubs_hierarchy_guard();
CREATE FUNCTION clubs_level_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='UPDATE' AND (NEW."ParentLevelId" IS DISTINCT FROM OLD."ParentLevelId" OR NEW."TenantId"<>OLD."TenantId") THEN RAISE EXCEPTION 'Existing level relationships are immutable' USING ERRCODE='23514'; END IF;
 IF NEW."Id"=NEW."ParentLevelId" THEN RAISE EXCEPTION 'Level cycle rejected' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER level_hierarchy_guard BEFORE INSERT OR UPDATE ON "Levels" FOR EACH ROW EXECUTE FUNCTION clubs_level_guard();

"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER organization_hierarchy_guard ON \"Orgs\"; DROP FUNCTION clubs_hierarchy_guard(); DROP TRIGGER level_hierarchy_guard ON \"Levels\"; DROP FUNCTION clubs_level_guard(); ALTER TABLE \"Orgs\" DROP CONSTRAINT \"CK_Org_Identity\", DROP CONSTRAINT \"CK_Org_Currency\"; DROP INDEX \"IX_Orgs_Name_Search\";"
            );
        }
    }
}
