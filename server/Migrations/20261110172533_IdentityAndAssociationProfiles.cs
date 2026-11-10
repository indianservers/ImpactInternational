using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IdentityAndAssociationProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Users_UserId",
                table: "Assignments"
            );

            migrationBuilder.DropForeignKey(name: "FK_Levels_Tenants_TenantId", table: "Levels");

            migrationBuilder.DropForeignKey(name: "FK_Mails_Users_UserId", table: "Mails");

            migrationBuilder.DropForeignKey(name: "FK_Orgs_Tenants_TenantId", table: "Orgs");

            migrationBuilder.DropForeignKey(name: "FK_Roles_Tenants_TenantId", table: "Roles");

            migrationBuilder.DropForeignKey(name: "FK_Sessions_Users_UserId", table: "Sessions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(
                    new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                    new TimeSpan(0, 0, 0, 0, 0)
                )
            );

            migrationBuilder.CreateTable(
                name: "Associations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Associations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Associations_Tenants_Id",
                        column: x => x.Id,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "AuthenticationAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationAudits", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "RegistrationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationRequests", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_Id",
                        column: x => x.Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationAudits_ActorId_At",
                table: "AuthenticationAudits",
                columns: new[] { "ActorId", "At" }
            );

            migrationBuilder.Sql(
                "INSERT INTO \"UserProfiles\" (\"Id\",\"Name\",\"Phone\") SELECT \"Id\",\"Name\",\"Phone\" FROM \"Users\"; INSERT INTO \"Associations\" (\"Id\",\"Name\",\"Currency\",\"TimeZone\",\"Language\",\"Color\") SELECT \"Id\",\"Name\",\"Currency\",\"TimeZone\",\"Language\",\"Color\" FROM \"Tenants\";"
            );
            migrationBuilder.DropColumn(name: "Name", table: "Users");
            migrationBuilder.DropColumn(name: "Phone", table: "Users");
            migrationBuilder.DropColumn(name: "Color", table: "Tenants");
            migrationBuilder.DropColumn(name: "Currency", table: "Tenants");
            migrationBuilder.DropColumn(name: "Language", table: "Tenants");
            migrationBuilder.DropColumn(name: "Name", table: "Tenants");
            migrationBuilder.DropColumn(name: "TimeZone", table: "Tenants");
            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_UserProfiles_UserId",
                table: "Assignments",
                column: "UserId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Levels_Associations_TenantId",
                table: "Levels",
                column: "TenantId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Mails_UserProfiles_UserId",
                table: "Mails",
                column: "UserId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Orgs_Associations_TenantId",
                table: "Orgs",
                column: "TenantId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Associations_TenantId",
                table: "Roles",
                column: "TenantId",
                principalTable: "Associations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_UserProfiles_UserId",
                table: "Sessions",
                column: "UserId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_UserProfiles_UserId",
                table: "Assignments"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Levels_Associations_TenantId",
                table: "Levels"
            );

            migrationBuilder.DropForeignKey(name: "FK_Mails_UserProfiles_UserId", table: "Mails");

            migrationBuilder.DropForeignKey(name: "FK_Orgs_Associations_TenantId", table: "Orgs");

            migrationBuilder.DropForeignKey(name: "FK_Roles_Associations_TenantId", table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_UserProfiles_UserId",
                table: "Sessions"
            );

            migrationBuilder.DropTable(name: "Associations");

            migrationBuilder.DropTable(name: "AuthenticationAudits");

            migrationBuilder.DropTable(name: "RegistrationRequests");

            migrationBuilder.DropTable(name: "UserProfiles");

            migrationBuilder.DropColumn(name: "CreatedAt", table: "Tenants");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Users_UserId",
                table: "Assignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Levels_Tenants_TenantId",
                table: "Levels",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Mails_Users_UserId",
                table: "Mails",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Orgs_Tenants_TenantId",
                table: "Orgs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Tenants_TenantId",
                table: "Roles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Users_UserId",
                table: "Sessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
