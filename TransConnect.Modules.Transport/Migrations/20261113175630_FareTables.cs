using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Transport.Migrations
{
    /// <inheritdoc />
    public partial class FareTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FareTables",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    VipSurcharge = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FareTables", x => x.Id);
                    table.CheckConstraint("CK_FareTables_Status", "[Status] IN ('Draft', 'Published', 'Superseded')");
                    table.CheckConstraint("CK_FareTables_Vip", "[VipSurcharge] >= 0");
                    table.ForeignKey(
                        name: "FK_FareTables_RouteRevisions_RouteRevisionId",
                        column: x => x.RouteRevisionId,
                        principalSchema: "transport",
                        principalTable: "RouteRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FareTables_Routes_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "transport",
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FarePrices",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FareTableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Price = table.Column<int>(type: "int", nullable: false),
                    VipPrice = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarePrices", x => x.Id);
                    table.CheckConstraint("CK_FarePrices_Amount", "[Price] > 0 AND ([VipPrice] IS NULL OR [VipPrice] > 0)");
                    table.ForeignKey(
                        name: "FK_FarePrices_FareTables_FareTableId",
                        column: x => x.FareTableId,
                        principalSchema: "transport",
                        principalTable: "FareTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarePrices_FareTableId_FromLocationId_ToLocationId",
                schema: "transport",
                table: "FarePrices",
                columns: new[] { "FareTableId", "FromLocationId", "ToLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FareTables_OneDraft",
                schema: "transport",
                table: "FareTables",
                column: "RouteRevisionId",
                unique: true,
                filter: "[Status] = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_FareTables_RouteId_Status",
                schema: "transport",
                table: "FareTables",
                columns: new[] { "RouteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FareTables_RouteRevisionId_TableNo",
                schema: "transport",
                table: "FareTables",
                columns: new[] { "RouteRevisionId", "TableNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarePrices",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "FareTables",
                schema: "transport");
        }
    }
}
