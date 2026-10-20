using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Compliance.Migrations
{
    /// <inheritdoc />
    public partial class ProfileDocumentDemoVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileDocumentId",
                schema: "compliance",
                table: "DemoDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                schema: "compliance",
                table: "DemoDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DemoProfileDocuments",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScopeOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastVersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoProfileDocuments", x => x.Id);
                    table.CheckConstraint("CK_DemoProfileDocuments_Type", "([SubjectType] = 'Driver' AND [DocumentType] = 'DriverLicense' AND [ScopeOrganizationId] IS NULL) OR ([SubjectType] = 'Vehicle' AND [DocumentType] = 'VehicleRegistration' AND [ScopeOrganizationId] IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoDocuments_ProfileDocumentId_VersionNumber",
                schema: "compliance",
                table: "DemoDocuments",
                columns: new[] { "ProfileDocumentId", "VersionNumber" },
                unique: true,
                filter: "[ProfileDocumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DemoProfileDocuments_SubjectType_SubjectId_DocumentType",
                schema: "compliance",
                table: "DemoProfileDocuments",
                columns: new[] { "SubjectType", "SubjectId", "DocumentType" },
                unique: true,
                filter: "[ScopeOrganizationId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DemoProfileDocuments_SubjectType_SubjectId_DocumentType_ScopeOrganizationId",
                schema: "compliance",
                table: "DemoProfileDocuments",
                columns: new[] { "SubjectType", "SubjectId", "DocumentType", "ScopeOrganizationId" },
                unique: true,
                filter: "[ScopeOrganizationId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DemoDocuments_DemoProfileDocuments_ProfileDocumentId",
                schema: "compliance",
                table: "DemoDocuments",
                column: "ProfileDocumentId",
                principalSchema: "compliance",
                principalTable: "DemoProfileDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DemoDocuments_DemoProfileDocuments_ProfileDocumentId",
                schema: "compliance",
                table: "DemoDocuments");

            migrationBuilder.DropTable(
                name: "DemoProfileDocuments",
                schema: "compliance");

            migrationBuilder.DropIndex(
                name: "IX_DemoDocuments_ProfileDocumentId_VersionNumber",
                schema: "compliance",
                table: "DemoDocuments");

            migrationBuilder.DropColumn(
                name: "ProfileDocumentId",
                schema: "compliance",
                table: "DemoDocuments");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                schema: "compliance",
                table: "DemoDocuments");
        }
    }
}
