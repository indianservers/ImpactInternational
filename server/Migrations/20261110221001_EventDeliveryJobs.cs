using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class EventDeliveryJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventNotices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Audience = table.Column<string>(type: "text", nullable: false),
                    Cursor = table.Column<Guid>(type: "uuid", nullable: true),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventNotices", x => x.Id);
                    table.UniqueConstraint(
                        "AK_EventNotices_TenantId_Id",
                        x => new { x.TenantId, x.Id }
                    );
                    table.ForeignKey(
                        name: "FK_EventNotices_Associations_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Associations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_EventNotices_Events_TenantId_EventId",
                        columns: x => new { x.TenantId, x.EventId },
                        principalTable: "Events",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventNotices_Completed_EventId",
                table: "EventNotices",
                columns: new[] { "Completed", "EventId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EventNotices_TenantId_EventId",
                table: "EventNotices",
                columns: new[] { "TenantId", "EventId" }
            );
            migrationBuilder.Sql(
                """
ALTER TABLE "Events" DROP CONSTRAINT "CK_Event_Config";
ALTER TABLE "Events" ADD CONSTRAINT "CK_Event_Config" CHECK ("StartsAt"<"EndsAt" AND "RegistrationStartsAt"<"RegistrationEndsAt" AND "RegistrationEndsAt"<="EndsAt" AND "Capacity">0 AND "MaxCompanions" BETWEEN 0 AND 10 AND "Approval" IN ('Draft','Pending Approval','Changes Requested','Rejected','Approved') AND "Publication" IN ('Draft','Published') AND "Lifecycle" IN ('Scheduled','In Progress','Completed','Cancelled','Archived'));
CREATE INDEX IF NOT EXISTS "IX_EventInvitations_EventId_PersonId" ON "EventInvitations" ("EventId","PersonId");
CREATE INDEX IF NOT EXISTS "IX_EventRegistrations_EventId_MembershipId" ON "EventRegistrations" ("EventId","MembershipId");
"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EventNotices");
        }
    }
}
