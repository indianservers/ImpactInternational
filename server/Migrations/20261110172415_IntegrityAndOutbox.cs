using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IntegrityAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "Mails",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "Mails",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                )
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SentAt",
                table: "Mails",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Mails_NextAttemptAt",
                table: "Mails",
                column: "NextAttemptAt",
                filter: "\"SentAt\" IS NULL AND NOT \"Used\""
            );

            migrationBuilder.CreateIndex(name: "IX_Mails_UserId", table: "Mails", column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Mails_Users_UserId",
                table: "Mails",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Mails_Users_UserId", table: "Mails");

            migrationBuilder.DropIndex(name: "IX_Mails_NextAttemptAt", table: "Mails");

            migrationBuilder.DropIndex(name: "IX_Mails_UserId", table: "Mails");

            migrationBuilder.DropColumn(name: "Attempts", table: "Mails");

            migrationBuilder.DropColumn(name: "NextAttemptAt", table: "Mails");

            migrationBuilder.DropColumn(name: "SentAt", table: "Mails");
        }
    }
}
