using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clubs.Api.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationOutboxDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TemplateParameters",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "Transactional",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DeliveryStatus",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "Mails",
                type: "text",
                nullable: false,
                defaultValue: "Email",
                oldClrType: typeof(string),
                oldType: "text");
            migrationBuilder.Sql("""
UPDATE "Mails" SET "DeadLetter"=false,"Attempts"=0,"NextAttemptAt"=now(),"LastError"='' WHERE "Channel"='' AND "SentAt" IS NULL AND "LastError"='InvalidOperationException';
UPDATE "Mails" SET "Channel"='Email' WHERE "Channel"='';
UPDATE "Mails" SET "Purpose"='Transactional' WHERE "Purpose"='';
UPDATE "Mails" SET "DeliveryStatus"=CASE WHEN "SentAt" IS NOT NULL THEN 'Accepted' WHEN "Suppressed" THEN 'Suppressed' ELSE 'Pending' END WHERE "DeliveryStatus"='' OR ("Channel"='Email' AND "LastError"='' AND "DeliveryStatus"='Failed' AND NOT "DeadLetter");
UPDATE "Mails" SET "TemplateParameters"='[]' WHERE "TemplateParameters"='';
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TemplateParameters",
                table: "Mails",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "[]");

            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                table: "Mails",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Transactional");

            migrationBuilder.AlterColumn<string>(
                name: "DeliveryStatus",
                table: "Mails",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "Mails",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Email");
        }
    }
}
