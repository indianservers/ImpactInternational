using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class DuesFollowupQueues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DuesCollections_TenantId_ClubId_Reference",
                table: "DuesCollections");

            migrationBuilder.CreateTable(
                name: "DuesFollowups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    NextFollowUp = table.Column<DateOnly>(type: "date", nullable: true),
                    PromisedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PromisedAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuesFollowups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuesFollowups_FinanceInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "FinanceInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuesFollowups_UserProfiles_ActorId",
                        column: x => x.ActorId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuesCollections_TenantId_ClubId_Reference",
                table: "DuesCollections",
                columns: new[] { "TenantId", "ClubId", "Reference" },
                unique: true,
                filter: "\"Status\" IN ('Submitted','Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_DuesFollowups_ActorId",
                table: "DuesFollowups",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_DuesFollowups_InvoiceId_CreatedAt",
                table: "DuesFollowups",
                columns: new[] { "InvoiceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DuesFollowups_TenantId_ActorId_RequestKey",
                table: "DuesFollowups",
                columns: new[] { "TenantId", "ActorId", "RequestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DuesFollowups");

            migrationBuilder.DropIndex(
                name: "IX_DuesCollections_TenantId_ClubId_Reference",
                table: "DuesCollections");

            migrationBuilder.CreateIndex(
                name: "IX_DuesCollections_TenantId_ClubId_Reference",
                table: "DuesCollections",
                columns: new[] { "TenantId", "ClubId", "Reference" },
                unique: true);
        }
    }
}
