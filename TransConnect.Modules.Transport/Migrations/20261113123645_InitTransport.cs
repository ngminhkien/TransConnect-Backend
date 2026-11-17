using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TransConnect.Modules.Transport.Migrations
{
    /// <inheritdoc />
    public partial class InitTransport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "transport");

            migrationBuilder.CreateTable(
                name: "SeatLayouts",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LayoutKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeckCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceLayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatLayouts", x => x.Id);
                    table.CheckConstraint("CK_SeatLayouts_Decks", "[DeckCount] BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_SeatLayouts_Kind", "[LayoutKind] IN ('Seater', 'Sleeper', 'Cabin')");
                    table.CheckConstraint("CK_SeatLayouts_Status", "[Status] IN ('Draft', 'Locked', 'Archived')");
                });

            migrationBuilder.CreateTable(
                name: "ServiceTypes",
                schema: "transport",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AllowedModes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsOperational = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "SeatLayoutCells",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeckNo = table.Column<int>(type: "int", nullable: false),
                    Row = table.Column<int>(type: "int", nullable: false),
                    Col = table.Column<int>(type: "int", nullable: false),
                    CellType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SeatClass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsSellable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatLayoutCells", x => x.Id);
                    table.CheckConstraint("CK_SeatLayoutCells_Type", "[CellType] IN ('Seat', 'Aisle', 'Empty', 'Driver', 'Door')");
                    table.ForeignKey(
                        name: "FK_SeatLayoutCells_SeatLayouts_LayoutId",
                        column: x => x.LayoutId,
                        principalSchema: "transport",
                        principalTable: "SeatLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeatLayoutDecks",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeckNo = table.Column<int>(type: "int", nullable: false),
                    Rows = table.Column<int>(type: "int", nullable: false),
                    Columns = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatLayoutDecks", x => x.Id);
                    table.CheckConstraint("CK_SeatLayoutDecks_Size", "[Rows] BETWEEN 1 AND 20 AND [Columns] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_SeatLayoutDecks_SeatLayouts_LayoutId",
                        column: x => x.LayoutId,
                        principalSchema: "transport",
                        principalTable: "SeatLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleSeatLayouts",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttachedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleSeatLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleSeatLayouts_SeatLayouts_LayoutId",
                        column: x => x.LayoutId,
                        principalSchema: "transport",
                        principalTable: "SeatLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRegistrations",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceTypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScopeNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRegistrations", x => x.Id);
                    table.CheckConstraint("CK_ServiceRegistrations_Status", "[Status] IN ('Pending', 'Approved', 'Rejected', 'Suspended', 'Withdrawn')");
                    table.ForeignKey(
                        name: "FK_ServiceRegistrations_ServiceTypes_ServiceTypeCode",
                        column: x => x.ServiceTypeCode,
                        principalSchema: "transport",
                        principalTable: "ServiceTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CurrentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.CheckConstraint("CK_Routes_Status", "[Status] IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "FK_Routes_ServiceRegistrations_ServiceRegistrationId",
                        column: x => x.ServiceRegistrationId,
                        principalSchema: "transport",
                        principalTable: "ServiceRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRegistrationEvents",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ScopeNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRegistrationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceRegistrationEvents_ServiceRegistrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalSchema: "transport",
                        principalTable: "ServiceRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RouteRevisions",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteRevisions", x => x.Id);
                    table.CheckConstraint("CK_RouteRevisions_Status", "[Status] IN ('Draft', 'Published', 'Retired')");
                    table.ForeignKey(
                        name: "FK_RouteRevisions_Routes_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "transport",
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RouteRevisionStops",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    SharedLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationVersion = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AddressDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProvinceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WardCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: true),
                    AllowPickup = table.Column<bool>(type: "bit", nullable: false),
                    AllowDropoff = table.Column<bool>(type: "bit", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    DwellMinutes = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteRevisionStops", x => x.Id);
                    table.CheckConstraint("CK_RouteRevisionStops_Offsets", "[Sequence] >= 1 AND [OffsetMinutes] >= 0 AND ([DwellMinutes] IS NULL OR [DwellMinutes] >= 0)");
                    table.ForeignKey(
                        name: "FK_RouteRevisionStops_RouteRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "transport",
                        principalTable: "RouteRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartureMinutes = table.Column<int>(type: "int", nullable: false),
                    DaysOfWeek = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedVehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParticipationMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReplacesScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                    table.CheckConstraint("CK_Schedules_Mode", "[ParticipationMode] IN ('InformationOnly', 'RequestConfirmation', 'InstantBooking')");
                    table.CheckConstraint("CK_Schedules_Range", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_Schedules_Status", "[Status] IN ('Draft', 'Active', 'Paused', 'Ended')");
                    table.CheckConstraint("CK_Schedules_Time", "[DepartureMinutes] BETWEEN 0 AND 1439 AND [DaysOfWeek] BETWEEN 1 AND 127");
                    table.ForeignKey(
                        name: "FK_Schedules_RouteRevisions_RouteRevisionId",
                        column: x => x.RouteRevisionId,
                        principalSchema: "transport",
                        principalTable: "RouteRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Schedules_Routes_RouteId",
                        column: x => x.RouteId,
                        principalSchema: "transport",
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleVieGoSeats",
                schema: "transport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeatLabel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleVieGoSeats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleVieGoSeats_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalSchema: "transport",
                        principalTable: "Schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "transport",
                table: "ServiceTypes",
                columns: new[] { "Code", "AllowedModes", "Description", "IsOperational", "Name", "SortOrder" },
                values: new object[,]
                {
                    { "CHARTER", "InformationOnly,RequestConfirmation", "Khách gửi yêu cầu, nhà xe báo giá, hai bên xác nhận. Chưa mở vận hành.", false, "Thuê nguyên xe / xe hợp đồng", 3 },
                    { "FIXED_ROUTE_COACH", "InformationOnly,RequestConfirmation,InstantBooking", "Chở khách theo tuyến, điểm dừng và lịch chạy đã công bố.", true, "Xe khách tuyến cố định", 1 },
                    { "FREIGHT_TRUCK", "InformationOnly,RequestConfirmation", "Đăng chuyến theo ngày, nhận hàng lẻ hoặc nguyên chuyến. Chưa mở vận hành.", false, "Xe tải chở hàng / ghép hàng", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RouteRevisions_OneDraft",
                schema: "transport",
                table: "RouteRevisions",
                column: "RouteId",
                unique: true,
                filter: "[Status] = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_RouteRevisions_RouteId_RevisionNo",
                schema: "transport",
                table: "RouteRevisions",
                columns: new[] { "RouteId", "RevisionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteRevisionStops_RevisionId_Sequence",
                schema: "transport",
                table: "RouteRevisionStops",
                columns: new[] { "RevisionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteRevisionStops_RevisionId_SharedLocationId",
                schema: "transport",
                table: "RouteRevisionStops",
                columns: new[] { "RevisionId", "SharedLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_OrganizationId_Code",
                schema: "transport",
                table: "Routes",
                columns: new[] { "OrganizationId", "Code" },
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_ServiceRegistrationId",
                schema: "transport",
                table: "Routes",
                column: "ServiceRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_OrganizationId_PlannedVehicleId",
                schema: "transport",
                table: "Schedules",
                columns: new[] { "OrganizationId", "PlannedVehicleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_OrganizationId_RouteId_Status",
                schema: "transport",
                table: "Schedules",
                columns: new[] { "OrganizationId", "RouteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_RouteId",
                schema: "transport",
                table: "Schedules",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_RouteRevisionId",
                schema: "transport",
                table: "Schedules",
                column: "RouteRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleVieGoSeats_ScheduleId_SeatLabel",
                schema: "transport",
                table: "ScheduleVieGoSeats",
                columns: new[] { "ScheduleId", "SeatLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatLayoutCells_LayoutId_DeckNo_Row_Col",
                schema: "transport",
                table: "SeatLayoutCells",
                columns: new[] { "LayoutId", "DeckNo", "Row", "Col" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatLayoutCells_LayoutId_Label",
                schema: "transport",
                table: "SeatLayoutCells",
                columns: new[] { "LayoutId", "Label" },
                unique: true,
                filter: "[Label] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeatLayoutDecks_LayoutId_DeckNo",
                schema: "transport",
                table: "SeatLayoutDecks",
                columns: new[] { "LayoutId", "DeckNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatLayouts_OrganizationId",
                schema: "transport",
                table: "SeatLayouts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRegistrationEvents_RegistrationId_CreatedAt",
                schema: "transport",
                table: "ServiceRegistrationEvents",
                columns: new[] { "RegistrationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRegistrations_OrganizationId_ServiceTypeCode",
                schema: "transport",
                table: "ServiceRegistrations",
                columns: new[] { "OrganizationId", "ServiceTypeCode" },
                unique: true,
                filter: "[Status] <> 'Withdrawn' AND [DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRegistrations_ServiceTypeCode",
                schema: "transport",
                table: "ServiceRegistrations",
                column: "ServiceTypeCode");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRegistrations_Status_ServiceTypeCode",
                schema: "transport",
                table: "ServiceRegistrations",
                columns: new[] { "Status", "ServiceTypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleSeatLayouts_LayoutId",
                schema: "transport",
                table: "VehicleSeatLayouts",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleSeatLayouts_OrganizationId_VehicleId",
                schema: "transport",
                table: "VehicleSeatLayouts",
                columns: new[] { "OrganizationId", "VehicleId" },
                unique: true,
                filter: "[EndedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteRevisionStops",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "ScheduleVieGoSeats",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "SeatLayoutCells",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "SeatLayoutDecks",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "ServiceRegistrationEvents",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "VehicleSeatLayouts",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "Schedules",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "SeatLayouts",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "RouteRevisions",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "Routes",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "ServiceRegistrations",
                schema: "transport");

            migrationBuilder.DropTable(
                name: "ServiceTypes",
                schema: "transport");
        }
    }
}
