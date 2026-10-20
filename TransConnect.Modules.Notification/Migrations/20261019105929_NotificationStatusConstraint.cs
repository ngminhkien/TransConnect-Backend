using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Notification.Migrations
{
    /// <inheritdoc />
    public partial class NotificationStatusConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [notification].[EmailMessages] WHERE [Status] NOT IN (0,1,2,3))
                    THROW 51006, 'Resolve invalid email statuses before migrating.', 1;
                """);
            migrationBuilder.AddCheckConstraint(
                name: "CK_EmailMessages_Status",
                schema: "notification",
                table: "EmailMessages",
                sql: "[Status] IN (0, 1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmailMessages_Status",
                schema: "notification",
                table: "EmailMessages");
        }
    }
}
