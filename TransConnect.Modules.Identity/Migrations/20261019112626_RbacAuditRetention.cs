using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class RbacAuditRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RbacAuditLogs",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubjectType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RbacAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RbacAuditLogs_CreatedAt",
                schema: "identity",
                table: "RbacAuditLogs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RbacAuditLogs",
                schema: "identity");
        }
    }
}
