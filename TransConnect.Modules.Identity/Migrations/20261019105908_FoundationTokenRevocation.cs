using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class FoundationTokenRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [identity].[Users] WHERE [AccountGroup] NOT IN ('Customer','Operator','InternalAdmin'))
                    THROW 51005, 'Resolve invalid account groups before migrating.', 1;
                """);
            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                schema: "identity",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevokedReason",
                schema: "identity",
                table: "RefreshTokens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AccountGroup",
                schema: "identity",
                table: "Users",
                sql: "[AccountGroup] IN ('Customer', 'Operator', 'InternalAdmin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AccountGroup",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                schema: "identity",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevokedReason",
                schema: "identity",
                table: "RefreshTokens");
        }
    }
}
