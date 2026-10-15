using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Compliance.Migrations
{
    /// <inheritdoc />
    public partial class InitComplianceDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compliance");

            migrationBuilder.CreateTable(
                name: "DemoDocuments",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Bucket = table.Column<string>(type: "nvarchar(63)", maxLength: 63, nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Scenario = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ScanResult = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OcrText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SimulatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoDocuments_AccountId_CreatedAt",
                schema: "compliance",
                table: "DemoDocuments",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DemoDocuments_Bucket_ObjectKey",
                schema: "compliance",
                table: "DemoDocuments",
                columns: new[] { "Bucket", "ObjectKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoDocuments",
                schema: "compliance");
        }
    }
}
