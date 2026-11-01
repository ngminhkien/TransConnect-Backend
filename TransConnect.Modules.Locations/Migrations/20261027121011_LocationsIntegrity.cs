using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Locations.Migrations
{
    /// <inheritdoc />
    public partial class LocationsIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_SharedLocationVersions_Coordinates",
                schema: "locations",
                table: "SharedLocationVersions",
                sql: "[Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SharedLocations_Confirmation",
                schema: "locations",
                table: "SharedLocations",
                sql: "[ConfirmationMethod] IS NOT NULL AND [ConfirmationMethod] IN ('MapPin', 'ManualCoordinates') AND [ConfirmedAt] IS NOT NULL AND [ConfirmedBy] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SharedLocations_Coordinates",
                schema: "locations",
                table: "SharedLocations",
                sql: "[Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerAddresses_Confirmation",
                schema: "locations",
                table: "CustomerAddresses",
                sql: "([ConfirmationMethod] IS NULL AND [ConfirmedAt] IS NULL AND [ConfirmedBy] IS NULL) OR ([ConfirmationMethod] IS NOT NULL AND [ConfirmationMethod] IN ('MapPin', 'ManualCoordinates') AND [ConfirmedAt] IS NOT NULL AND [ConfirmedBy] IS NOT NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerAddresses_Coordinates",
                schema: "locations",
                table: "CustomerAddresses",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeRequests_Kind",
                schema: "locations",
                table: "ChangeRequests",
                sql: "[Kind] IN ('Create', 'Update', 'Archive')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeRequests_Status",
                schema: "locations",
                table: "ChangeRequests",
                sql: "[Status] IN ('Pending', 'Approved', 'Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SharedLocationVersions_Coordinates",
                schema: "locations",
                table: "SharedLocationVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SharedLocations_Confirmation",
                schema: "locations",
                table: "SharedLocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SharedLocations_Coordinates",
                schema: "locations",
                table: "SharedLocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerAddresses_Confirmation",
                schema: "locations",
                table: "CustomerAddresses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerAddresses_Coordinates",
                schema: "locations",
                table: "CustomerAddresses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeRequests_Kind",
                schema: "locations",
                table: "ChangeRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeRequests_Status",
                schema: "locations",
                table: "ChangeRequests");
        }
    }
}
