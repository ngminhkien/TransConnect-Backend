using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransConnect.Modules.Operator.Migrations
{
    /// <inheritdoc />
    public partial class ApplicantOnboardingAndReviewHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "operator",
                table: "Organizations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProfileReviews",
                schema: "operator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileReviews_SubjectType_SubjectId_CreatedAt",
                schema: "operator",
                table: "ProfileReviews",
                columns: new[] { "SubjectType", "SubjectId", "CreatedAt" });
            // Preserve the last known legacy decision; never invent applicants or earlier reviews.
            migrationBuilder.Sql("""
                INSERT INTO [operator].[ProfileReviews] ([Id],[SubjectType],[SubjectId],[Status],[Note],[ActorAccountId],[CreatedAt])
                SELECT NEWID(), N'Organization', [Id], [ReviewStatus], [ReviewNote], [ReviewedBy], [ReviewedAt]
                FROM [operator].[Organizations] WHERE [ReviewedBy] IS NOT NULL AND [ReviewedAt] IS NOT NULL;
                INSERT INTO [operator].[ProfileReviews] ([Id],[SubjectType],[SubjectId],[Status],[Note],[ActorAccountId],[CreatedAt])
                SELECT NEWID(), N'Driver', [Id], [ReviewStatus], [ReviewNote], [ReviewedBy], [ReviewedAt]
                FROM [operator].[Drivers] WHERE [ReviewedBy] IS NOT NULL AND [ReviewedAt] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileReviews",
                schema: "operator");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "operator",
                table: "Organizations");
        }
    }
}
