using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class FourLevelTrainingActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActivityClubId",
                table: "Events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivityLevel",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActivityType",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChiefGuest",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoordinatorEmail",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoordinatorPhone",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MeetingCost",
                table: "Events",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MemberParticipants",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OutsideVolunteers",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProjectCoordinator",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TrainingPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPrograms", x => x.Id);
                    table.UniqueConstraint("AK_TrainingPrograms_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_TrainingPrograms_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberCertifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<string>(type: "text", nullable: false),
                    Batch = table.Column<string>(type: "text", nullable: false),
                    Issuer = table.Column<string>(type: "text", nullable: false),
                    TrainingCenter = table.Column<string>(type: "text", nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    VerificationUrl = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberCertifications", x => x.Id);
                    table.UniqueConstraint("AK_MemberCertifications_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_MemberCertifications_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberCertifications_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberCertifications_TrainingPrograms_TenantId_ProgramId",
                        columns: x => new { x.TenantId, x.ProgramId },
                        principalTable: "TrainingPrograms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberCertifications_PersonId",
                table: "MemberCertifications",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberCertifications_TenantId_PersonId",
                table: "MemberCertifications",
                columns: new[] { "TenantId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberCertifications_TenantId_ProgramId_CertificateId",
                table: "MemberCertifications",
                columns: new[] { "TenantId", "ProgramId", "CertificateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPrograms_TenantId_Name",
                table: "TrainingPrograms",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            // Preserve legacy units and appointments as historical records. Never promote
            // their holders to a larger region scope automatically.
            migrationBuilder.Sql("""
                CREATE TABLE "HierarchyRetirementArchive" ("Id" uuid PRIMARY KEY, "Snapshot" jsonb NOT NULL);
                INSERT INTO "HierarchyRetirementArchive" SELECT o."Id", to_jsonb(o) FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."Name" IN ('District','Zone');
                CREATE TEMP TABLE retired_ancestors ON COMMIT DROP AS
                WITH RECURSIVE chain AS (
                    SELECT o."Id" AS old_id,o."TenantId" AS tenant,o."ParentId" AS ancestor,1 AS depth
                    FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."Name" IN ('District','Zone')
                    UNION ALL SELECT c.old_id,c.tenant,p."ParentId",c.depth+1 FROM chain c JOIN "Orgs" p ON p."Id"=c.ancestor AND p."TenantId"=c.tenant WHERE c.depth<20
                ) SELECT DISTINCT ON (c.old_id) c.old_id,c.tenant,p."Id" AS parent
                  FROM chain c JOIN "Orgs" p ON p."Id"=c.ancestor AND p."TenantId"=c.tenant JOIN "Levels" l ON l."Id"=p."LevelId"
                  WHERE l."Name" NOT IN ('District','Zone') ORDER BY c.old_id,c.depth;
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" WHERE l."Name" IN ('District','Zone') AND NOT EXISTS(SELECT 1 FROM retired_ancestors a WHERE a.old_id=o."Id"))
                    THEN RAISE EXCEPTION 'A legacy unit has no surviving ancestor. Repair its hierarchy before migration.'; END IF;
                END $$;
                ALTER TABLE "Levels" DISABLE TRIGGER level_hierarchy_guard;
                UPDATE "Levels" club SET "ParentLevelId"=region."Id" FROM "Levels" region WHERE club."Name"='Club' AND region."Name"='Region' AND region."TenantId"=club."TenantId";
                ALTER TABLE "Levels" ENABLE TRIGGER level_hierarchy_guard;
                UPDATE "Orgs" o SET "ParentId"=a.parent,"Version"=o."Version"+1 FROM retired_ancestors a,"Levels" l WHERE o."ParentId"=a.old_id AND o."TenantId"=a.tenant AND l."Id"=o."LevelId" AND l."Name" NOT IN ('District','Zone');
                UPDATE "Orgs" o SET "Status"='Archived',"Version"=o."Version"+1 FROM "Levels" l WHERE o."LevelId"=l."Id" AND l."Name" IN ('District','Zone');
                INSERT INTO "MembershipStageRoles"("Id","TenantId","StageId","RoleId")
                    SELECT DISTINCT gen_random_uuid(),sr."TenantId",sr."StageId",region."Id" FROM "MembershipStageRoles" sr JOIN "Roles" old ON old."Id"=sr."RoleId" JOIN "Roles" region ON region."TenantId"=sr."TenantId" AND region."Name"='Regional President'
                    WHERE old."Name" IN ('District President','Zonal President','Zone President') ON CONFLICT("StageId","RoleId") DO NOTHING;
                DELETE FROM "MembershipStageRoles" sr USING "Roles" old WHERE sr."RoleId"=old."Id" AND old."Name" IN ('District President','Zonal President','Zone President');
                UPDATE "MembershipApprovalStages" SET "Name"=replace(replace(replace("Name",'zone president','region president'),'Zone President','Region President'),'district president','region president');
                UPDATE "OfficerPositions" p SET "Active"=false FROM "Levels" l WHERE p."LevelId"=l."Id" AND l."Name" IN ('District','Zone');
                UPDATE "OfficerAppointments" p SET "Status"='Revoked',"Version"=p."Version"+1 FROM retired_ancestors a WHERE p."OrgId"=a.old_id AND p."Status" IN ('Active','Approved','Scheduled','Proposed','Pending approval');
                UPDATE "Events" e SET "OrgId"=a.parent FROM retired_ancestors a WHERE e."OrgId"=a.old_id;
                UPDATE "Events" e SET "ActivityType"=CASE WHEN t."Name" IN ('Service','Leadership','Business','Training','Nari Cell') THEN t."Name" ELSE 'Training' END,
                    "ActivityLevel"=CASE WHEN l."Name" IN ('Club','Region','National') THEN l."Name" ELSE 'National' END,
                    "ActivityClubId"=CASE WHEN l."IsClub" THEN o."Id" ELSE NULL END
                    FROM "EventTypes" t,"Orgs" o,"Levels" l WHERE e."TypeId"=t."Id" AND e."OrgId"=o."Id" AND o."LevelId"=l."Id";
                INSERT INTO "EventTypes"("Id","TenantId","Name","Active","DefaultCapacity")
                    SELECT gen_random_uuid(),t."Id",kind,true,100 FROM "Tenants" t CROSS JOIN unnest(ARRAY['Service','Leadership','Business','Training','Nari Cell']) kind
                    WHERE NOT EXISTS(SELECT 1 FROM "EventTypes" e WHERE e."TenantId"=t."Id" AND e."Name"=kind);
                UPDATE "EventTypes" SET "Active"=false WHERE "Name" NOT IN ('Service','Leadership','Business','Training','Nari Cell');
                WITH numbered AS (SELECT c."Id",r."Name",row_number() OVER (PARTITION BY c."ParentId" ORDER BY c."Code") AS n FROM "Orgs" c JOIN "Orgs" r ON r."Id"=c."ParentId" JOIN "Levels" l ON l."Id"=c."LevelId" WHERE l."IsClub" AND c."Code" LIKE 'DEMO-%' AND c."Name" LIKE '% District % Zone %')
                    UPDATE "Orgs" c SET "Name"=n."Name" || ' Club ' || lpad(n.n::text,3,'0') FROM numbered n WHERE c."Id"=n."Id";
                DO $$ DECLARE r record; BEGIN
                  FOR r IN SELECT table_name FROM information_schema.columns WHERE table_schema='public' AND column_name='Purpose' LOOP
                    EXECUTE format('UPDATE %I SET "Purpose"=''RegionalDues'' WHERE "Purpose" IN (''DistrictDues'',''ZonalDues'')',r.table_name);
                  END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Restore the pre-migration database backup to recover the previous hierarchy. Automatic rollback would lose certification and activity records.");
        }
    }
}
