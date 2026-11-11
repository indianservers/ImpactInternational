using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class IdCardPolicyDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultTemplateVersionId",
                table: "IdCardPolicies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdCardPolicies_TenantId_DefaultTemplateVersionId",
                table: "IdCardPolicies",
                columns: new[] { "TenantId", "DefaultTemplateVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_IdCardPolicies_IdCardTemplateVersions_TenantId_DefaultTempl~",
                table: "IdCardPolicies",
                columns: new[] { "TenantId", "DefaultTemplateVersionId" },
                principalTable: "IdCardTemplateVersions",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdCardPolicies_IdCardTemplateVersions_TenantId_DefaultTempl~",
                table: "IdCardPolicies");

            migrationBuilder.DropIndex(
                name: "IX_IdCardPolicies_TenantId_DefaultTemplateVersionId",
                table: "IdCardPolicies");

            migrationBuilder.DropColumn(
                name: "DefaultTemplateVersionId",
                table: "IdCardPolicies");
        }
    }
}
