using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TransConnect.Modules.Identity.Persistence;
using Xunit;

namespace TransConnect.Tests;

public class MigrationTests
{
    [Fact]
    public void TC_DB_004_Operator_migration_preflights_conflicts_and_preserves_relationship_history()
    {
        using var db = new TransConnect.Modules.Operator.Persistence.OperatorDbContext(
            new DbContextOptionsBuilder<TransConnect.Modules.Operator.Persistence.OperatorDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var migrations = db.Database.GetMigrations().ToList();
        var index = migrations.IndexOf("20261019105924_FoundationAccountAndRelationshipRules");
        var script = db.GetService<IMigrator>().GenerateScript(migrations[index - 1], migrations[index]);
        Assert.Contains("Resolve existing Owner/Driver account conflicts", script);
        Assert.Contains("Resolve duplicate active Owners", script);
        Assert.Contains("WHERE [Role] = 'OrganizationOwner' AND [IsActive] = 1 AND [DeletedAt] IS NULL", script);
        Assert.Contains("[InvitationId] uniqueidentifier NULL", script);
        Assert.Contains("FK_Engagements_Invitations_InvitationId", script);
        Assert.Contains("[EndReason] nvarchar(1000) NULL", script);
        Assert.Contains("[EndedBy] uniqueidentifier NULL", script);
        Assert.DoesNotContain("DELETE FROM", script);
        Assert.DoesNotContain("UPDATE [operator]", script);
    }

    [Fact]
    public void TC_DB_003_Revocation_migration_adds_nullable_metadata_without_fabricating_history()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        // The migration itself is inspected through its bounded script, independent of later migrations.
        var migrations = db.Database.GetMigrations().ToList();
        var index = migrations.IndexOf("20261019105908_FoundationTokenRevocation");
        var script = db.GetService<IMigrator>().GenerateScript(migrations[index - 1], migrations[index]);
        Assert.Contains("[RevokedAt] datetime2 NULL", script);
        Assert.Contains("[RevokedReason] nvarchar(100) NULL", script);
        Assert.Contains("CK_Users_AccountGroup", script);
        Assert.DoesNotContain("UPDATE [identity].[RefreshTokens]", script);
    }

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
