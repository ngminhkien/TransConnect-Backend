using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Operator.Migrations
{
    /// <inheritdoc />
    public partial class MinimalProfilesAndEngagementSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PermittedPayloadKg",
                schema: "operator",
                table: "Vehicles",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatCount",
                schema: "operator",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleKind",
                schema: "operator",
                table: "Vehicles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "operator",
                table: "Organizations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                schema: "operator",
                table: "Organizations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalEntityType",
                schema: "operator",
                table: "Organizations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisteredAddress",
                schema: "operator",
                table: "Organizations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverAddressSnapshot",
                schema: "operator",
                table: "Engagements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverNameSnapshot",
                schema: "operator",
                table: "Engagements",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverPhoneSnapshot",
                schema: "operator",
                table: "Engagements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactAddress",
                schema: "operator",
                table: "Drivers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                schema: "operator",
                table: "Drivers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_ProfileCapacity",
                schema: "operator",
                table: "Vehicles",
                sql: "([VehicleKind] IS NULL AND [SeatCount] IS NULL AND [PermittedPayloadKg] IS NULL) OR ([VehicleKind] IS NOT NULL AND [VehicleKind] = 'Passenger' AND [SeatCount] IS NOT NULL AND [SeatCount] BETWEEN 1 AND 1000 AND [PermittedPayloadKg] IS NULL) OR ([VehicleKind] IS NOT NULL AND [VehicleKind] = 'Freight' AND [PermittedPayloadKg] IS NOT NULL AND CAST([PermittedPayloadKg] AS decimal(12,2)) > 0 AND CAST([PermittedPayloadKg] AS decimal(12,2)) <= 1000000 AND [SeatCount] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_ProfileCapacity",
                schema: "operator",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PermittedPayloadKg",
                schema: "operator",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "SeatCount",
                schema: "operator",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleKind",
                schema: "operator",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "operator",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                schema: "operator",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LegalEntityType",
                schema: "operator",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "RegisteredAddress",
                schema: "operator",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DriverAddressSnapshot",
                schema: "operator",
                table: "Engagements");

            migrationBuilder.DropColumn(
                name: "DriverNameSnapshot",
                schema: "operator",
                table: "Engagements");

            migrationBuilder.DropColumn(
                name: "DriverPhoneSnapshot",
                schema: "operator",
                table: "Engagements");

            migrationBuilder.DropColumn(
                name: "ContactAddress",
                schema: "operator",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                schema: "operator",
                table: "Drivers");
        }
    }
}
