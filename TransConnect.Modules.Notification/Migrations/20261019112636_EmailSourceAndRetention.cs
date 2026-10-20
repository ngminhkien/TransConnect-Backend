using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Notification.Migrations
{
    /// <inheritdoc />
    public partial class EmailSourceAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BodyPurgedAt",
                schema: "notification",
                table: "EmailMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliverBefore",
                schema: "notification",
                table: "EmailMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DispatchLease",
                schema: "notification",
                table: "EmailMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EligibilityCheckedAt",
                schema: "notification",
                table: "EmailMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionCategory",
                schema: "notification",
                table: "EmailMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                schema: "notification",
                table: "EmailMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                schema: "notification",
                table: "EmailMessages",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TerminalAt",
                schema: "notification",
                table: "EmailMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_Status_TerminalAt",
                schema: "notification",
                table: "EmailMessages",
                columns: new[] { "Status", "TerminalAt" });

            // Stop old workers before applying. Legacy secret emails have no verifiable source ID:
            // terminate them and require a fresh OTP/invitation rather than guessing their source.
            migrationBuilder.Sql("""
                UPDATE [notification].[EmailMessages] SET [RetentionCategory] = CASE
                    WHEN [TemplateCode] IN ('email-verification','password-reset') THEN 'Otp'
                    WHEN [TemplateCode] = 'organization-invitation' THEN 'Invitation'
                    ELSE 'General' END, [LastError] = NULL;
                UPDATE [notification].[EmailMessages] SET [Status] = 3,
                    [ProcessingStartedAt] = NULL, [NextAttemptAt] = NULL
                    WHERE [Status] IN (0,1) AND [RetentionCategory] IN ('Otp','Invitation') AND [SourceId] IS NULL;
                UPDATE [notification].[EmailMessages] SET [BodyHtml] = N'', [BodyText] = N'',
                    [TerminalAt] = COALESCE([SentAt], SYSUTCDATETIME()), [BodyPurgedAt] = SYSUTCDATETIME()
                    WHERE [Status] IN (2,3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_Status_TerminalAt",
                schema: "notification",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "BodyPurgedAt",
                schema: "notification",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "DeliverBefore",
                schema: "notification",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "DispatchLease",
                schema: "notification",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "EligibilityCheckedAt",
                schema: "notification",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "RetentionCategory",
                schema: "notification",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "SourceId",
                schema: "notification",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "SourceType",
                schema: "notification",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "TerminalAt",
                schema: "notification",
                table: "EmailMessages");
        }
    }
}
