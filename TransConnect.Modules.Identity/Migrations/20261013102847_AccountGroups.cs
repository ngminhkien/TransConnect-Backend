using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AccountGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                schema: "identity",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "AccountGroup",
                schema: "identity",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Customer");

            // Preserve existing IDs/passwords; only existing active Admin role holders
            // move into the internal realm. Other legacy accounts remain Customer.
            migrationBuilder.Sql("""
                UPDATE u SET [AccountGroup] = N'InternalAdmin'
                FROM [identity].[Users] u
                WHERE EXISTS (
                    SELECT 1 FROM [identity].[UserRoles] ur
                    JOIN [identity].[Roles] r ON r.[Id] = ur.[RoleId]
                    WHERE ur.[UserId] = u.[Id] AND r.[Name] = N'Admin' AND r.[DeletedAt] IS NULL);
                IF EXISTS (
                    SELECT 1 FROM [identity].[Users]
                    GROUP BY [AccountGroup], LOWER(LTRIM(RTRIM([Email])))
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Resolve normalized email duplicates before applying AccountGroups.', 1;
                UPDATE [identity].[Users] SET [Email] = LOWER(LTRIM(RTRIM([Email])));
                UPDATE [identity].[RefreshTokens] SET [IsRevoked] = 1 WHERE [IsRevoked] = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AccountGroup_Email",
                schema: "identity",
                table: "Users",
                columns: new[] { "AccountGroup", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AccountGroup_GoogleId",
                schema: "identity",
                table: "Users",
                columns: new[] { "AccountGroup", "GoogleId" },
                unique: true,
                filter: "[GoogleId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [identity].[Users] GROUP BY [Email] HAVING COUNT(*) > 1)
                    OR EXISTS (SELECT 1 FROM [identity].[Users] WHERE [GoogleId] IS NOT NULL GROUP BY [GoogleId] HAVING COUNT(*) > 1)
                    THROW 51001, 'Cannot remove account groups while cross-group identities exist.', 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Users_AccountGroup_Email",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_AccountGroup_GoogleId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccountGroup",
                schema: "identity",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                schema: "identity",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "[GoogleId] IS NOT NULL");
        }
    }
}
