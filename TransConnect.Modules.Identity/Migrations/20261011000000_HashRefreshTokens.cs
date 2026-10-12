using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Migrations;

// Data-only migration: the existing indexed nvarchar(500) column is unchanged.
[DbContext(typeof(IdentityDbContext))]
[Migration("20261011000000_HashRefreshTokens")]
public class HashRefreshTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Legacy tokens are Base64 ASCII. VARCHAR ensures the same bytes as .NET UTF-8.
        migrationBuilder.Sql("""
            UPDATE [identity].[RefreshTokens]
            SET [Token] = N'sha256:' + CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(500), [Token])), 2)
            WHERE [Token] NOT LIKE N'sha256:%';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Không thể khôi phục refresh token từ hash. Xem docs/auth-and-verification.md trước khi rollback.");
}
