using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Operator.Migrations
{
    /// <inheritdoc />
    public partial class AuditContextAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "operator",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                schema: "operator",
                table: "AuditLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                schema: "operator",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.Sql("""
                UPDATE [operator].[AuditLogs] SET [OrganizationId] = NULL
                    WHERE [OrganizationId] = '00000000-0000-0000-0000-000000000000';
                UPDATE [operator].[AuditLogs] SET [SubjectType] = CASE
                    WHEN [Action] IN ('member.invite','member.invitation.revoke','driver.email-invite') THEN 'OrganizationInvitation'
                    WHEN [Action] IN ('member.accept','member.update') THEN 'OrganizationMember'
                    WHEN [Action] = 'driver.review' THEN 'Driver'
                    WHEN [Action] IN ('driver.invite','driver.accept','driver.end') THEN 'DriverEngagement'
                    WHEN [Action] LIKE 'organization.%' THEN 'Organization'
                    WHEN [Action] LIKE 'vehicle.%' THEN 'OrganizationVehicle'
                    ELSE 'Unknown' END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                schema: "operator",
                table: "AuditLogs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CreatedAt",
                schema: "operator",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RequestId",
                schema: "operator",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                schema: "operator",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "operator",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
