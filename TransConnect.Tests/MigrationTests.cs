using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TransConnect.Modules.Identity.Persistence;
using Xunit;

namespace TransConnect.Tests;

public class MigrationTests
{
    [Fact]
    public void TC_DB_002_Rbac_unique_indexes_only_cover_active_rows()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("20261011000000_HashRefreshTokens");
        Assert.Contains("CREATE UNIQUE INDEX [IX_Roles_Name] ON [identity].[Roles] ([Name]) WHERE [DeletedAt] IS NULL", script);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Permissions_Code] ON [identity].[Permissions] ([Code]) WHERE [DeletedAt] IS NULL", script);
        Assert.DoesNotContain("DROP TABLE", script);
    }

    [Fact]
    public void TC_DB_001_Hash_migration_generates_sql_without_model_drift()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript(
            "20260801172249_AddVerificationCodesAndEmailVerified", "20261011000000_HashRefreshTokens");
        Assert.Contains("HASHBYTES('SHA2_256', CONVERT(varchar(500), [Token]))", script);
        Assert.Contains("WHERE [Token] NOT LIKE N'sha256:%'", script);
        Assert.DoesNotContain("DROP TABLE", script);
    }
}
