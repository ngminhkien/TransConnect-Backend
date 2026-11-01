using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Locations.Migrations
{
    /// <inheritdoc />
    public partial class InitLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "locations");

            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                schema: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaseVersion = table.Column<int>(type: "int", nullable: true),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WardCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AddressDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AccessInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfirmationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Datasets",
                schema: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceCommit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    License = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Artifact = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Datasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                schema: "locations",
                columns: table => new
                {
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => new { x.DatasetId, x.Code });
                    table.ForeignKey(
                        name: "FK_Provinces_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalSchema: "locations",
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Wards",
                schema: "locations",
                columns: table => new
                {
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => new { x.DatasetId, x.Code });
                    table.UniqueConstraint("AK_Wards_DatasetId_ProvinceCode_Code", x => new { x.DatasetId, x.ProvinceCode, x.Code });
                    table.ForeignKey(
                        name: "FK_Wards_Provinces_DatasetId_ProvinceCode",
                        columns: x => new { x.DatasetId, x.ProvinceCode },
                        principalSchema: "locations",
                        principalTable: "Provinces",
                        principalColumns: new[] { "DatasetId", "Code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                schema: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WardCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AddressDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AccessInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConfirmationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Wards_DatasetId_ProvinceCode_WardCode",
                        columns: x => new { x.DatasetId, x.ProvinceCode, x.WardCode },
                        principalSchema: "locations",
                        principalTable: "Wards",
                        principalColumns: new[] { "DatasetId", "ProvinceCode", "Code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SharedLocations",
                schema: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WardCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AddressDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AccessInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfirmationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedLocations_Wards_DatasetId_ProvinceCode_WardCode",
                        columns: x => new { x.DatasetId, x.ProvinceCode, x.WardCode },
                        principalSchema: "locations",
                        principalTable: "Wards",
                        principalColumns: new[] { "DatasetId", "ProvinceCode", "Code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SharedLocationVersions",
                schema: "locations",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WardCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AddressDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AccessInstructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfirmationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedLocationVersions", x => new { x.LocationId, x.Version });
                    table.ForeignKey(
                        name: "FK_SharedLocationVersions_SharedLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "locations",
                        principalTable: "SharedLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedLocationVersions_Wards_DatasetId_ProvinceCode_WardCode",
                        columns: x => new { x.DatasetId, x.ProvinceCode, x.WardCode },
                        principalSchema: "locations",
                        principalTable: "Wards",
                        principalColumns: new[] { "DatasetId", "ProvinceCode", "Code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_OrganizationId_Status",
                schema: "locations",
                table: "ChangeRequests",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId_IsActive",
                schema: "locations",
                table: "CustomerAddresses",
                columns: new[] { "CustomerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_DatasetId_ProvinceCode_WardCode",
                schema: "locations",
                table: "CustomerAddresses",
                columns: new[] { "DatasetId", "ProvinceCode", "WardCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_IsActive",
                schema: "locations",
                table: "Datasets",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_Source_Version",
                schema: "locations",
                table: "Datasets",
                columns: new[] { "Source", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedLocations_DatasetId_ProvinceCode_WardCode",
                schema: "locations",
                table: "SharedLocations",
                columns: new[] { "DatasetId", "ProvinceCode", "WardCode" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedLocations_IsActive_ProvinceCode_Type",
                schema: "locations",
                table: "SharedLocations",
                columns: new[] { "IsActive", "ProvinceCode", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedLocationVersions_DatasetId_ProvinceCode_WardCode",
                schema: "locations",
                table: "SharedLocationVersions",
                columns: new[] { "DatasetId", "ProvinceCode", "WardCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeRequests",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "CustomerAddresses",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "SharedLocationVersions",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "SharedLocations",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "Wards",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "Provinces",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "Datasets",
                schema: "locations");
        }
    }
}
