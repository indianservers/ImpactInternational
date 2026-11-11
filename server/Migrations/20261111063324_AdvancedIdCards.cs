using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdvancedIdCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_DigitalCards_MembershipId", table: "DigitalCards");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "DigitalCards",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiryAt",
                table: "DigitalCards",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Files",
                table: "DigitalCards",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesId",
                table: "DigitalCards",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "DigitalCards",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Serial",
                table: "DigitalCards",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Snapshot",
                table: "DigitalCards",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "DigitalCards",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateVersionId",
                table: "DigitalCards",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "DigitalCards",
                type: "bigint",
                nullable: false,
                defaultValue: 0L
            );

            migrationBuilder.CreateTable(
                name: "IdCardPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowMemberDownload = table.Column<bool>(type: "boolean", nullable: false),
                    AllowClubDesign = table.Column<bool>(type: "boolean", nullable: false),
                    ValidityMonths = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdCardPolicies", x => x.Id);
                    table.UniqueConstraint(
                        "AK_IdCardPolicies_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_IdCardPolicies_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "IdCardPrintPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Layout = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdCardPrintPresets", x => x.Id);
                    table.UniqueConstraint(
                        "AK_IdCardPrintPresets_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_IdCardPrintPresets_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_IdCardPrintPresets_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "IdCardTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    Layout = table.Column<string>(type: "text", nullable: false),
                    PublisherId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdCardTemplateVersions", x => x.Id);
                    table.UniqueConstraint(
                        "AK_IdCardTemplateVersions_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_IdCardTemplateVersions_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_IdCardTemplateVersions_CertificateTemplates_TenantId_Templa~",
                        columns: x => new { x.TenantId, x.TemplateId },
                        principalTable: "CertificateTemplates",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_IdCardTemplateVersions_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "IdCardJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<string>(type: "text", nullable: false),
                    PrintLayout = table.Column<string>(type: "text", nullable: false),
                    Reissue = table.Column<bool>(type: "boolean", nullable: false),
                    CancelRequested = table.Column<bool>(type: "boolean", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Success = table.Column<int>(type: "integer", nullable: false),
                    Failed = table.Column<int>(type: "integer", nullable: false),
                    OutputKey = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ExpiresAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdCardJobs", x => x.Id);
                    table.UniqueConstraint(
                        "AK_IdCardJobs_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_IdCardJobs_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_IdCardJobs_IdCardTemplateVersions_TenantId_TemplateVersionId",
                        columns: x => new { x.TenantId, x.TemplateVersionId },
                        principalTable: "IdCardTemplateVersions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_IdCardJobs_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "IdCardJobItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdCardJobItems", x => x.Id);
                    table.UniqueConstraint(
                        "AK_IdCardJobItems_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_IdCardJobItems_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_IdCardJobItems_IdCardJobs_TenantId_JobId",
                        columns: x => new { x.TenantId, x.JobId },
                        principalTable: "IdCardJobs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_IdCardJobItems_Memberships_TenantId_MembershipId",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "cards.bulk",
                    "cards.design",
                    "cards.issue",
                    "cards.publish",
                    "cards.settings",
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_MembershipId",
                table: "DigitalCards",
                column: "MembershipId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL AND \"State\" IN ('Legacy','Issued','Generated')"
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_Serial",
                table: "DigitalCards",
                column: "Serial",
                unique: true,
                filter: "\"Serial\"<>''"
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_TenantId_State_ExpiryAt",
                table: "DigitalCards",
                columns: new[] { "TenantId", "State", "ExpiryAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_TenantId_TemplateVersionId",
                table: "DigitalCards",
                columns: new[] { "TenantId", "TemplateVersionId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardJobItems_JobId_MembershipId",
                table: "IdCardJobItems",
                columns: new[] { "JobId", "MembershipId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardJobItems_JobId_Status_Ordinal",
                table: "IdCardJobItems",
                columns: new[] { "JobId", "Status", "Ordinal" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardJobItems_TenantId_JobId",
                table: "IdCardJobItems",
                columns: new[] { "TenantId", "JobId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardJobItems_TenantId_MembershipId",
                table: "IdCardJobItems",
                columns: new[] { "TenantId", "MembershipId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardJobs_Status_CreatedAt",
                table: "IdCardJobs",
                columns: new[] { "Status", "CreatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardJobs_TenantId_OrgId_UpdatedAt",
                table: "IdCardJobs",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardJobs_TenantId_TemplateVersionId",
                table: "IdCardJobs",
                columns: new[] { "TenantId", "TemplateVersionId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardPolicies_TenantId",
                table: "IdCardPolicies",
                column: "TenantId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardPrintPresets_TenantId_OrgId_UpdatedAt",
                table: "IdCardPrintPresets",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardTemplateVersions_TemplateId_Number",
                table: "IdCardTemplateVersions",
                columns: new[] { "TemplateId", "Number" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardTemplateVersions_TenantId_OrgId_UpdatedAt",
                table: "IdCardTemplateVersions",
                columns: new[] { "TenantId", "OrgId", "UpdatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IdCardTemplateVersions_TenantId_TemplateId",
                table: "IdCardTemplateVersions",
                columns: new[] { "TenantId", "TemplateId" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_DigitalCards_IdCardTemplateVersions_TenantId_TemplateVersio~",
                table: "DigitalCards",
                columns: new[] { "TenantId", "TemplateVersionId" },
                principalTable: "IdCardTemplateVersions",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict
            );
            migrationBuilder.Sql(
                """
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),r."Id",p."Id" FROM "Roles" r CROSS JOIN "Permissions" p WHERE p."Id" LIKE 'cards.%' AND (r."Name"='Association Super Admin' OR r."Name" IN ('Club Manager','Club Secretary','Club President') AND p."Id"<>'cards.settings') ON CONFLICT DO NOTHING;
CREATE FUNCTION clubs_idcard_version_immutable() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Published card template versions are immutable'; END $$;
CREATE TRIGGER idcard_version_immutable BEFORE UPDATE OR DELETE ON "IdCardTemplateVersions" FOR EACH ROW EXECUTE FUNCTION clubs_idcard_version_immutable();
CREATE FUNCTION clubs_idcard_snapshot_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF OLD."State" IN ('Pending Approval','Approved','Generated','Issued','Revoked','Replaced') AND (NEW."Snapshot"<>OLD."Snapshot" OR NEW."TemplateVersionId" IS DISTINCT FROM OLD."TemplateVersionId" OR NEW."MembershipId"<>OLD."MembershipId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."ExpiryAt" IS DISTINCT FROM OLD."ExpiryAt" OR NEW."TokenHash"<>OLD."TokenHash" OR NEW."ProtectedToken"<>OLD."ProtectedToken" OR NEW."Serial"<>OLD."Serial" OR NEW."ReplacesId" IS DISTINCT FROM OLD."ReplacesId") THEN RAISE EXCEPTION 'Reviewed card identity and snapshot are immutable'; END IF;
 IF OLD."State" IN ('Generated','Issued','Revoked','Replaced') AND NEW."Files"<>OLD."Files" THEN RAISE EXCEPTION 'Generated card file references are immutable'; END IF;
 RETURN NEW; END $$;
CREATE TRIGGER idcard_snapshot_guard BEFORE UPDATE ON "DigitalCards" FOR EACH ROW EXECUTE FUNCTION clubs_idcard_snapshot_guard();
UPDATE "DigitalCards" SET "State"='Legacy' WHERE "State"='';
ALTER TABLE "DigitalCards" ALTER COLUMN "State" SET DEFAULT 'Legacy';
ALTER TABLE "DigitalCards" ADD CONSTRAINT card_state_valid CHECK("State" IN ('Legacy','Draft','Pending Approval','Approved','Generated','Issued','Expired','Rejected','Revoked','Replaced'));
CREATE FUNCTION clubs_idcard_job_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."TemplateVersionId"<>OLD."TemplateVersionId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."OrgId"<>OLD."OrgId" OR NEW."CreatorId"<>OLD."CreatorId" OR NEW."Total"<>OLD."Total" OR NEW."Format"<>OLD."Format" OR NEW."PrintLayout"<>OLD."PrintLayout" OR NEW."Reissue"<>OLD."Reissue" THEN RAISE EXCEPTION 'Generation selection and configuration are immutable'; END IF;RETURN NEW;END $$;
CREATE TRIGGER idcard_job_guard BEFORE UPDATE ON "IdCardJobs" FOR EACH ROW EXECUTE FUNCTION clubs_idcard_job_guard();
CREATE FUNCTION clubs_idcard_item_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF NEW."MembershipId"<>OLD."MembershipId" OR NEW."JobId"<>OLD."JobId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."Ordinal"<>OLD."Ordinal" THEN RAISE EXCEPTION 'Generation item identity is immutable'; END IF;RETURN NEW;END $$;
CREATE TRIGGER idcard_item_guard BEFORE UPDATE ON "IdCardJobItems" FOR EACH ROW EXECUTE FUNCTION clubs_idcard_item_guard();
"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
DELETE FROM "RolePermissions" WHERE "PermissionId" LIKE 'cards.%';
DROP TRIGGER IF EXISTS idcard_snapshot_guard ON "DigitalCards";
ALTER TABLE "DigitalCards" DROP CONSTRAINT IF EXISTS card_state_valid;
DROP FUNCTION IF EXISTS clubs_idcard_snapshot_guard();
DROP TRIGGER IF EXISTS idcard_item_guard ON "IdCardJobItems";
DROP TRIGGER IF EXISTS idcard_job_guard ON "IdCardJobs";
DROP TRIGGER IF EXISTS idcard_version_immutable ON "IdCardTemplateVersions";
DROP FUNCTION IF EXISTS clubs_idcard_item_guard();DROP FUNCTION IF EXISTS clubs_idcard_job_guard();DROP FUNCTION IF EXISTS clubs_idcard_version_immutable();
"""
            );
            migrationBuilder.DropForeignKey(
                name: "FK_DigitalCards_IdCardTemplateVersions_TenantId_TemplateVersio~",
                table: "DigitalCards"
            );

            migrationBuilder.DropTable(name: "IdCardJobItems");

            migrationBuilder.DropTable(name: "IdCardPolicies");

            migrationBuilder.DropTable(name: "IdCardPrintPresets");

            migrationBuilder.DropTable(name: "IdCardJobs");

            migrationBuilder.DropTable(name: "IdCardTemplateVersions");

            migrationBuilder.DropIndex(name: "IX_DigitalCards_MembershipId", table: "DigitalCards");

            migrationBuilder.DropIndex(name: "IX_DigitalCards_Serial", table: "DigitalCards");

            migrationBuilder.DropIndex(
                name: "IX_DigitalCards_TenantId_State_ExpiryAt",
                table: "DigitalCards"
            );

            migrationBuilder.DropIndex(
                name: "IX_DigitalCards_TenantId_TemplateVersionId",
                table: "DigitalCards"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "cards.bulk"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "cards.design"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "cards.issue"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "cards.publish"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "cards.settings"
            );

            migrationBuilder.DropColumn(name: "CreatorId", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "ExpiryAt", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "Files", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "ReplacesId", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "RequestId", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "Serial", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "Snapshot", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "State", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "TemplateVersionId", table: "DigitalCards");

            migrationBuilder.DropColumn(name: "Version", table: "DigitalCards");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCards_MembershipId",
                table: "DigitalCards",
                column: "MembershipId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL"
            );
        }
    }
}
