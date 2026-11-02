using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Modules.Operator.Entities;
using TransConnect.Modules.Operator.Persistence;
using TransConnect.Modules.Operator.Services;
using Xunit;

namespace TransConnect.Tests;

public sealed class LocationOrganizationAccessTests
{
    [Theory]
    [InlineData("OrganizationOwner", true, "Approved", true)]
    [InlineData("Manager", true, "Approved", false)]
    [InlineData("Dispatcher", true, "Approved", false)]
    [InlineData("OrganizationOwner", false, "Approved", false)]
    [InlineData("OrganizationOwner", true, "Pending", false)]
    public async Task Owner_adapter_uses_live_membership_and_organization(string role, bool active, string review, bool allowed)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new OperatorDbContext(new DbContextOptionsBuilder<OperatorDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var actor = Guid.NewGuid();
        var organization = new Organization { Name = "Test", RegistrationNumber = "LOC-TEST", ReviewStatus = review };
        db.Organizations.Add(organization);
        db.Members.Add(new OrganizationMember { OrganizationId = organization.Id, AccountId = actor, Role = role, IsActive = active });
        await db.SaveChangesAsync();
        var accounts = new Accounts();
        var access = new LocationOrganizationAccess(db, accounts);
        if (allowed)
        {
            await access.RequireOwnerAsync(organization.Id, actor);
            accounts.Active = false;
            await Assert.ThrowsAsync<ForbiddenAppException>(() => access.RequireOwnerAsync(organization.Id, actor));
        }
        else await Assert.ThrowsAsync<ForbiddenAppException>(() => access.RequireOwnerAsync(organization.Id, actor));
        accounts.Active = true;
        await Assert.ThrowsAsync<ForbiddenAppException>(() => access.RequireOwnerAsync(organization.Id, Guid.NewGuid()));
    }

    private sealed class Accounts : IAccountDirectory
    {
        public bool Active { get; set; } = true;
        public Task<AccountSummary?> FindActiveAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountSummary?>(Active ? new(accountId, "owner@example.com", AccountGroups.Operator, true) : null);
    }
}
