using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Operator.Migrations
{
    /// <inheritdoc />
    public partial class FoundationAccountAndRelationshipRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [operator].[Members] m JOIN [operator].[Drivers] d
                    ON d.[AccountId] = m.[AccountId] WHERE m.[Role] = N'OrganizationOwner')
                    THROW 51002, 'Resolve existing Owner/Driver account conflicts before migrating; do not transfer history automatically.', 1;
                IF EXISTS (SELECT [OrganizationId] FROM [operator].[Members]
                    WHERE [Role] = N'OrganizationOwner' AND [IsActive] = 1 AND [DeletedAt] IS NULL
                    GROUP BY [OrganizationId] HAVING COUNT(*) > 1)
                    THROW 51003, 'Resolve duplicate active Owners before migrating.', 1;
                IF EXISTS (SELECT 1 FROM [operator].[Members] WHERE [Role] NOT IN ('OrganizationOwner','Manager','Dispatcher'))
                    OR EXISTS (SELECT 1 FROM [operator].[Invitations] WHERE [Role] NOT IN ('OrganizationOwner','Manager','Dispatcher','Driver'))
                    OR EXISTS (SELECT 1 FROM [operator].[Organizations] WHERE [ReviewStatus] NOT IN ('Pending','Approved','Rejected'))
                    OR EXISTS (SELECT 1 FROM [operator].[Drivers] WHERE [ReviewStatus] NOT IN ('Pending','Approved','Rejected'))
                    THROW 51004, 'Resolve invalid Operator roles/review statuses before migrating.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Members_OrganizationId",
                schema: "operator",
                table: "Members");

            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                schema: "operator",
                table: "OrganizationVehicles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EndedBy",
                schema: "operator",
                table: "OrganizationVehicles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitationId",
                schema: "operator",
                table: "Engagements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Organizations_ReviewStatus",
                schema: "operator",
                table: "Organizations",
                sql: "[ReviewStatus] IN ('Pending', 'Approved', 'Rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_Members_OrganizationId",
                schema: "operator",
                table: "Members",
                column: "OrganizationId",
                unique: true,
                filter: "[Role] = 'OrganizationOwner' AND [IsActive] = 1 AND [DeletedAt] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Members_Role",
                schema: "operator",
                table: "Members",
                sql: "[Role] IN ('OrganizationOwner', 'Manager', 'Dispatcher')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invitations_Role",
                schema: "operator",
                table: "Invitations",
                sql: "[Role] IN ('OrganizationOwner', 'Manager', 'Dispatcher', 'Driver')");

            migrationBuilder.CreateIndex(
                name: "IX_Engagements_InvitationId",
                schema: "operator",
                table: "Engagements",
                column: "InvitationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Drivers_ReviewStatus",
                schema: "operator",
                table: "Drivers",
                sql: "[ReviewStatus] IN ('Pending', 'Approved', 'Rejected')");

            migrationBuilder.AddForeignKey(
                name: "FK_Engagements_Invitations_InvitationId",
                schema: "operator",
                table: "Engagements",
                column: "InvitationId",
                principalSchema: "operator",
                principalTable: "Invitations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Engagements_Invitations_InvitationId",
                schema: "operator",
                table: "Engagements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Organizations_ReviewStatus",
                schema: "operator",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Members_OrganizationId",
                schema: "operator",
                table: "Members");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Members_Role",
                schema: "operator",
                table: "Members");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invitations_Role",
                schema: "operator",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Engagements_InvitationId",
                schema: "operator",
                table: "Engagements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Drivers_ReviewStatus",
                schema: "operator",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "EndReason",
                schema: "operator",
                table: "OrganizationVehicles");

            migrationBuilder.DropColumn(
                name: "EndedBy",
                schema: "operator",
                table: "OrganizationVehicles");

            migrationBuilder.DropColumn(
                name: "InvitationId",
                schema: "operator",
                table: "Engagements");

            migrationBuilder.CreateIndex(
                name: "IX_Members_OrganizationId",
                schema: "operator",
                table: "Members",
                column: "OrganizationId");
        }
    }
}
