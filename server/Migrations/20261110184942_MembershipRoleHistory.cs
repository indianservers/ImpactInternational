using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class MembershipRoleHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleAssignmentHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "text", nullable: false),
                    OrganizationName = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignmentHistory", x => x.Id);
                    table.UniqueConstraint(
                        "AK_RoleAssignmentHistory_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_RoleAssignmentHistory_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_RoleAssignmentHistory_Orgs_TenantId_OrgId",
                        columns: x => new { x.TenantId, x.OrgId },
                        principalTable: "Orgs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_RoleAssignmentHistory_Roles_TenantId_RoleId",
                        columns: x => new { x.TenantId, x.RoleId },
                        principalTable: "Roles",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignmentHistory_TenantId_OrgId",
                table: "RoleAssignmentHistory",
                columns: new[] { "TenantId", "OrgId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignmentHistory_TenantId_RoleId",
                table: "RoleAssignmentHistory",
                columns: new[] { "TenantId", "RoleId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignmentHistory_UserId_TenantId_At",
                table: "RoleAssignmentHistory",
                columns: new[] { "UserId", "TenantId", "At" }
            );
            migrationBuilder.Sql(
                """
CREATE OR REPLACE FUNCTION clubs_membership_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(hashtext(NEW."TenantId"::text));
 IF NOT EXISTS(SELECT 1 FROM "Orgs" o JOIN "Levels" l ON o."LevelId"=l."Id" AND o."TenantId"=l."TenantId" WHERE o."Id"=NEW."ClubId" AND o."TenantId"=NEW."TenantId" AND l."IsClub") THEN RAISE EXCEPTION 'Membership target must be a club in its association' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND (NEW."TenantId"<>OLD."TenantId" OR NEW."PersonId"<>OLD."PersonId" OR NEW."ClubId"<>OLD."ClubId") THEN RAISE EXCEPTION 'Affiliation identity is immutable; use a transfer' USING ERRCODE='23514'; END IF;
 IF NEW."Status" IN ('Active','Inactive','Suspended','Expired') AND EXISTS(SELECT 1 FROM "MembershipPolicies" p WHERE p."TenantId"=NEW."TenantId" AND NOT p."AllowMultipleClubs") AND EXISTS(SELECT 1 FROM "Memberships" m WHERE m."TenantId"=NEW."TenantId" AND m."PersonId"=NEW."PersonId" AND m."ClubId"<>NEW."ClubId" AND m."Id"<>NEW."Id" AND m."Status" IN ('Active','Inactive','Suspended','Expired')) THEN RAISE EXCEPTION 'Single-club association policy violated' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE INDEX "IX_Persons_DisplayName_Id" ON "Persons" ("DisplayName","Id");
CREATE INDEX "IX_Membership_Expiry_Due" ON "Memberships" ("EndsAt","Id") WHERE "Status" IN ('Active','Inactive') AND "EndsAt" IS NOT NULL;
CREATE FUNCTION clubs_role_assignment_history() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE item record; verb text;
BEGIN
 IF TG_OP='DELETE' THEN item:=OLD;verb:='Revoked';ELSE item:=NEW;verb:='Granted';END IF;
 INSERT INTO "RoleAssignmentHistory" ("Id","TenantId","AssignmentId","UserId","RoleId","OrgId","RoleName","OrganizationName","Action","At") SELECT gen_random_uuid(),item."TenantId",item."Id",item."UserId",item."RoleId",item."OrgId",r."Name",o."Name",verb,now() FROM "Roles" r JOIN "Orgs" o ON o."Id"=item."OrgId" AND o."TenantId"=item."TenantId" WHERE r."Id"=item."RoleId";
 RETURN NULL;
END $$;
CREATE TRIGGER role_assignment_history AFTER INSERT OR DELETE ON "Assignments" FOR EACH ROW EXECUTE FUNCTION clubs_role_assignment_history();
INSERT INTO "RoleAssignmentHistory" ("Id","TenantId","AssignmentId","UserId","RoleId","OrgId","RoleName","OrganizationName","Action","At") SELECT gen_random_uuid(),a."TenantId",a."Id",a."UserId",a."RoleId",a."OrgId",r."Name",o."Name",'Existing assignment snapshot',now() FROM "Assignments" a JOIN "Roles" r ON r."Id"=a."RoleId" JOIN "Orgs" o ON o."Id"=a."OrgId";
CREATE TRIGGER role_assignment_history_immutable BEFORE UPDATE OR DELETE ON "RoleAssignmentHistory" FOR EACH ROW EXECUTE FUNCTION clubs_membership_history_guard();

"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER role_assignment_history ON \"Assignments\";DROP FUNCTION clubs_role_assignment_history();DROP INDEX \"IX_Persons_DisplayName_Id\";DROP INDEX \"IX_Membership_Expiry_Due\";"
            );
            migrationBuilder.DropTable(name: "RoleAssignmentHistory");
        }
    }
}
