using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IntelligenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "CommunicationExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "DefinitionSnapshot",
                table: "CommunicationExecutions",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: "CommunicationExecutions",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedResourceId",
                table: "CommunicationExecutions",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "CommunicationExecutions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                )
            );

            migrationBuilder.AddColumn<Guid>(
                name: "OrgId",
                table: "CommunicationExecutions",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "RuleVersion",
                table: "CommunicationExecutions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L
            );

            migrationBuilder.AddColumn<int>(
                name: "Step",
                table: "CommunicationExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "Trace",
                table: "CommunicationExecutions",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Definition",
                table: "CommunicationAutomations",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.CreateTable(
                name: "IntelligenceAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    QueryHash = table.Column<string>(type: "text", nullable: false),
                    Tool = table.Column<string>(type: "text", nullable: false),
                    Results = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntelligenceAudits", x => x.Id);
                    table.UniqueConstraint(
                        "AK_IntelligenceAudits_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_IntelligenceAudits_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "KnowledgePassages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersion = table.Column<int>(type: "integer", nullable: false),
                    Page = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(
                        type: "tsvector",
                        nullable: false,
                        computedColumnSql: "to_tsvector('simple', \"Text\")",
                        stored: true
                    ),
                    IndexedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IndexedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgePassages", x => x.Id);
                    table.UniqueConstraint(
                        "AK_KnowledgePassages_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_KnowledgePassages_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_KnowledgePassages_GovernanceDocuments_TenantId_DocumentId",
                        columns: x => new { x.TenantId, x.DocumentId },
                        principalTable: "GovernanceDocuments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TenantBrandings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: false),
                    Favicon = table.Column<string>(type: "text", nullable: false),
                    Accent = table.Column<string>(type: "text", nullable: false),
                    ClubTerm = table.Column<string>(type: "text", nullable: false),
                    DistrictTerm = table.Column<string>(type: "text", nullable: false),
                    Welcome = table.Column<string>(type: "text", nullable: false),
                    Languages = table.Column<string>(type: "text", nullable: false),
                    DisabledFeatures = table.Column<string>(type: "text", nullable: false),
                    RemoteAiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AiPolicy = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBrandings", x => x.Id);
                    table.UniqueConstraint(
                        "AK_TenantBrandings_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_TenantBrandings_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TenantDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    Challenge = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    VerifiedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    LastCheckedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDomains", x => x.Id);
                    table.UniqueConstraint(
                        "AK_TenantDomains_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_TenantDomains_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.InsertData(
                table: "Permissions",
                column: "Id",
                values: new object[]
                {
                    "intelligence.assistant",
                    "intelligence.automations",
                    "intelligence.knowledge",
                    "intelligence.view",
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationExecutions_TenantId_OrgId",
                table: "CommunicationExecutions",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IntelligenceAudits_TenantId_OrgId_At",
                table: "IntelligenceAudits",
                columns: new[] { "TenantId", "OrgId", "At" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgePassages_DocumentId_DocumentVersion_Page",
                table: "KnowledgePassages",
                columns: new[] { "DocumentId", "DocumentVersion", "Page" },
                unique: true
            );

            migrationBuilder
                .CreateIndex(
                    name: "IX_KnowledgePassages_SearchVector",
                    table: "KnowledgePassages",
                    column: "SearchVector"
                )
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgePassages_TenantId_DocumentId_DocumentVersion",
                table: "KnowledgePassages",
                columns: new[] { "TenantId", "DocumentId", "DocumentVersion" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TenantBrandings_TenantId",
                table: "TenantBrandings",
                column: "TenantId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TenantDomains_Host",
                table: "TenantDomains",
                column: "Host",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CommunicationExecutions_Orgs_TenantId_OrgId",
                table: "CommunicationExecutions",
                columns: new[] { "TenantId", "OrgId" },
                principalTable: "Orgs",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict
            );
            migrationBuilder.Sql(
                """

CREATE INDEX "IX_Workflow_ExecutionDue" ON "CommunicationExecutions" ("Status","NextAttemptAt") WHERE "DefinitionSnapshot"<>'';
CREATE INDEX "IX_Workflow_GeneratedResource" ON "CommunicationExecutions" ("GeneratedResourceId") WHERE "GeneratedResourceId" IS NOT NULL;
CREATE OR REPLACE FUNCTION clubs_intelligence_immutable() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Intelligence audit history is append only';END $$;
CREATE TRIGGER intelligence_audit_immutable BEFORE UPDATE OR DELETE ON "IntelligenceAudits" FOR EACH ROW EXECUTE FUNCTION clubs_intelligence_immutable();
ALTER TABLE "KnowledgePassages" ADD CONSTRAINT knowledge_page_bounds CHECK("Page" BETWEEN 1 AND 100 AND length("Text") BETWEEN 1 AND 30000 AND "DocumentVersion">0);
ALTER TABLE "TenantDomains" ADD CONSTRAINT domain_activation_guard CHECK("Host"=lower("Host") AND "Status" IN ('Pending','Verified','Active','Disabled') AND ("Status" NOT IN ('Verified','Active') OR "VerifiedAt" IS NOT NULL));
CREATE OR REPLACE FUNCTION clubs_workflow_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF TG_OP='DELETE' AND OLD."Definition"<>'' THEN RAISE EXCEPTION 'Workflow history cannot be deleted';END IF;
 IF TG_OP='DELETE' THEN RETURN OLD;END IF;
 IF OLD."Definition"<>'' AND OLD."Status"<>'Draft' THEN
 IF NEW."Status"='Draft' THEN RAISE EXCEPTION 'Reviewed workflow requires a new revision';END IF;
 IF (to_jsonb(NEW)-ARRAY['Active','Status','Version','StartsAt']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Active','Status','Version','StartsAt']) THEN RAISE EXCEPTION 'Reviewed workflow content is immutable';END IF;
 END IF; RETURN NEW;END $$;
CREATE TRIGGER intelligence_workflow_guard BEFORE UPDATE OR DELETE ON "CommunicationAutomations" FOR EACH ROW EXECUTE FUNCTION clubs_workflow_guard();
CREATE OR REPLACE FUNCTION clubs_workflow_execution_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
 IF OLD."DefinitionSnapshot"<>'' AND (NEW."DefinitionSnapshot"<>OLD."DefinitionSnapshot" OR NEW."RuleVersion"<>OLD."RuleVersion" OR NEW."AutomationId"<>OLD."AutomationId" OR NEW."TenantId"<>OLD."TenantId" OR NEW."SourceKey"<>OLD."SourceKey" OR NEW."Step"<OLD."Step" OR NOT NEW."Trace"::jsonb @> OLD."Trace"::jsonb) THEN RAISE EXCEPTION 'Execution snapshot and trace are immutable';END IF;RETURN NEW;END $$;
CREATE TRIGGER intelligence_execution_guard BEFORE UPDATE ON "CommunicationExecutions" FOR EACH ROW EXECUTE FUNCTION clubs_workflow_execution_guard();
CREATE INDEX "IX_Intelligence_ClubMeetings_Title" ON "ClubMeetings" USING gin("Title" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_OperationTasks_Title" ON "OperationTasks" USING gin("Title" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_ServiceProjects_Name" ON "ServiceProjects" USING gin("Name" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_GovernanceDocuments_Title" ON "GovernanceDocuments" USING gin("Title" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_FinanceInvoices_Number" ON "FinanceInvoices" USING gin("Number" gin_trgm_ops);
CREATE INDEX "IX_Intelligence_FinanceInvoices_Customer" ON "FinanceInvoices" USING gin("Customer" gin_trgm_ops);
INSERT INTO "Permissions" ("Id") VALUES ('intelligence.view') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'intelligence.view' FROM "Roles" WHERE "Name" IN ('Association Super Admin','Club President','Club Secretary','Club Treasurer','Club Manager') ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('intelligence.assistant') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'intelligence.assistant' FROM "Roles" ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('intelligence.knowledge') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'intelligence.knowledge' FROM "Roles" ON CONFLICT DO NOTHING;
INSERT INTO "Permissions" ("Id") VALUES ('intelligence.automations') ON CONFLICT DO NOTHING;
INSERT INTO "RolePermissions" ("Id","RoleId","PermissionId") SELECT gen_random_uuid(),"Id",'intelligence.automations' FROM "Roles" WHERE "Name" IN ('Association Super Admin','Club President','Club Secretary','Club Treasurer','Club Manager') ON CONFLICT DO NOTHING;

"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
DELETE FROM "RolePermissions" WHERE "PermissionId" LIKE 'intelligence.%';
DROP INDEX IF EXISTS "IX_Intelligence_ClubMeetings_Title";
DROP INDEX IF EXISTS "IX_Intelligence_OperationTasks_Title";
DROP INDEX IF EXISTS "IX_Intelligence_ServiceProjects_Name";
DROP INDEX IF EXISTS "IX_Intelligence_GovernanceDocuments_Title";
DROP INDEX IF EXISTS "IX_Intelligence_FinanceInvoices_Number";
DROP INDEX IF EXISTS "IX_Intelligence_FinanceInvoices_Customer";
DROP TRIGGER IF EXISTS intelligence_workflow_guard ON "CommunicationAutomations";
DROP TRIGGER IF EXISTS intelligence_execution_guard ON "CommunicationExecutions";
DROP INDEX IF EXISTS "IX_Workflow_ExecutionDue";
DROP INDEX IF EXISTS "IX_Workflow_GeneratedResource";
"""
            );
            migrationBuilder.DropForeignKey(
                name: "FK_CommunicationExecutions_Orgs_TenantId_OrgId",
                table: "CommunicationExecutions"
            );

            migrationBuilder.DropTable(name: "IntelligenceAudits");

            migrationBuilder.DropTable(name: "KnowledgePassages");

            migrationBuilder.DropTable(name: "TenantBrandings");

            migrationBuilder.DropTable(name: "TenantDomains");

            migrationBuilder.DropIndex(
                name: "IX_CommunicationExecutions_TenantId_OrgId",
                table: "CommunicationExecutions"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "intelligence.assistant"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "intelligence.automations"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "intelligence.knowledge"
            );

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: "intelligence.view"
            );

            migrationBuilder.DropColumn(name: "Attempts", table: "CommunicationExecutions");

            migrationBuilder.DropColumn(
                name: "DefinitionSnapshot",
                table: "CommunicationExecutions"
            );

            migrationBuilder.DropColumn(name: "Error", table: "CommunicationExecutions");

            migrationBuilder.DropColumn(
                name: "GeneratedResourceId",
                table: "CommunicationExecutions"
            );

            migrationBuilder.DropColumn(name: "NextAttemptAt", table: "CommunicationExecutions");

            migrationBuilder.DropColumn(name: "OrgId", table: "CommunicationExecutions");

            migrationBuilder.DropColumn(name: "RuleVersion", table: "CommunicationExecutions");

            migrationBuilder.DropColumn(name: "Step", table: "CommunicationExecutions");

            migrationBuilder.DropColumn(name: "Trace", table: "CommunicationExecutions");

            migrationBuilder.DropColumn(name: "Definition", table: "CommunicationAutomations");
        }
    }
}
