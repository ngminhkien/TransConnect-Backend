using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Transport;
using TransConnect.Modules.Locations.Dto.Requests;
using TransConnect.Modules.Locations.Persistence;
using TransConnect.Modules.Locations.Services;
using Xunit;

namespace TransConnect.Tests;

public sealed class LocationsTests
{
    [Fact]
    public async Task TC_LOC_001_Import_is_idempotent_and_rejects_changed_content()
    {
        using var f = await Fixture.Create();
        var first = await f.Service.ImportAsync(Dataset(), default);
        var second = await f.Service.ImportAsync(Dataset(), default);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(await f.Db.Datasets.ToListAsync());
        Assert.Equal("01", (await f.Service.ProvincesAsync(default)).Single().Code);
        var changed = Dataset(); changed.Wards[0].Name = "Changed";
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ImportAsync(changed, default));
        Assert.True((await f.Db.Datasets.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task TC_LOC_002_Invalid_upgrade_preserves_active_dataset_and_old_references()
    {
        using var f = await Fixture.Create();
        var old = await f.Service.ImportAsync(Dataset(), default);
        f.Actor.Group = AccountGroups.Customer;
        var address = await f.Service.SaveAddressAsync(null, Address(old.Id), default);
        f.Actor.Group = AccountGroups.InternalAdmin;
        var invalid = Dataset("v2"); invalid.Wards[0].ProvinceCode = "99";
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ImportAsync(invalid, default));
        Assert.Equal(old.Id, (await f.Service.ProvincesAsync(default)).Single().DatasetId);
        var newer = await f.Service.ImportAsync(Dataset("v2"), default);
        Assert.Equal(newer.Id, (await f.Service.ProvincesAsync(default)).Single().DatasetId);
        var repeatedOld = await f.Service.ImportAsync(Dataset(), default);
        Assert.False(repeatedOld.IsActive);
        Assert.Equal(newer.Id, (await f.Service.ActiveDatasetAsync(default)).Id);
        f.Actor.Group = AccountGroups.Customer;
        Assert.Equal(old.Id, (await f.Service.AddressAsync(address.Id, default)).DatasetId);
        Assert.Equal(2, await f.Db.Wards.CountAsync());
    }

    [Fact]
    public async Task TC_LOC_003_Private_address_can_omit_coordinates_and_cannot_leak_to_another_customer()
    {
        using var f = await Fixture.Create();
        var dataset = await f.Service.ImportAsync(Dataset(), default);
        f.Actor.Group = AccountGroups.Customer;
        var address = await f.Service.SaveAddressAsync(null, Address(dataset.Id), default);
        Assert.Null(address.Latitude); Assert.Null(address.ConfirmedAt);
        Assert.Empty((await f.Service.SharedAsync(null, null, null, 1, 20, default)).Items);
        f.Actor.Id = Guid.NewGuid();
        Assert.Empty(await f.Service.AddressesAsync(default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.AddressAsync(address.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.SaveAddressAsync(address.Id, Address(dataset.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.ArchiveAddressAsync(address.Id, 1, default));
    }

    [Theory]
    [InlineData(10d, null)]
    [InlineData(null, 20d)]
    [InlineData(91d, 20d)]
    [InlineData(10d, 181d)]
    public async Task TC_LOC_004_Invalid_coordinate_pair_is_rejected(double? latitude, double? longitude)
    {
        using var f = await Fixture.Create();
        var dataset = await f.Service.ImportAsync(Dataset(), default);
        f.Actor.Group = AccountGroups.Customer;
        var request = Address(dataset.Id);
        request.Latitude = (decimal?)latitude; request.Longitude = (decimal?)longitude;
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.SaveAddressAsync(null, request, default));
        Assert.Empty(await f.Db.Addresses.ToListAsync());
    }

    [Fact]
    public async Task TC_LOC_005_Confirmation_is_explicit_zero_is_valid_and_edit_invalidates_confirmation()
    {
        using var f = await Fixture.Create();
        var dataset = await f.Service.ImportAsync(Dataset(), default);
        f.Actor.Group = AccountGroups.Customer;
        var request = Address(dataset.Id); request.Latitude = 0; request.Longitude = 0;
        var address = await f.Service.SaveAddressAsync(null, request, default);
        Assert.Null(address.ConfirmedAt);
        request.Version = address.Version; request.ConfirmationMethod = "ManualCoordinates";
        address = await f.Service.SaveAddressAsync(address.Id, request, default);
        Assert.Equal(f.Actor.Id, address.ConfirmedBy);
        Assert.NotNull(address.ConfirmedAt);
        request.Version = address.Version; request.Label = "New label"; request.ConfirmationMethod = null;
        address = await f.Service.SaveAddressAsync(address.Id, request, default);
        Assert.NotNull(address.ConfirmedAt);
        request.Version = address.Version; request.AddressDetail = "Different gate";
        address = await f.Service.SaveAddressAsync(address.Id, request, default);
        Assert.Null(address.ConfirmedAt);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.SaveAddressAsync(address.Id, request, default));
    }

    [Fact]
    public async Task TC_LOC_006_Pending_and_rejected_requests_never_enter_public_catalogue()
    {
        using var f = await Fixture.Create();
        var dataset = await f.Service.ImportAsync(Dataset(), default);
        f.Actor.Group = AccountGroups.Operator;
        var request = await f.Service.SubmitAsync(Proposal(dataset.Id), default);
        Assert.Empty((await f.Service.SharedAsync(null, null, null, 1, 20, default)).Items);
        f.Actor.Group = AccountGroups.InternalAdmin;
        await f.Service.ReviewAsync(request.Id, new() { Version = 1, Approve = false, Reason = "Không đúng điểm phục vụ" }, default);
        Assert.Empty((await f.Service.SharedAsync(null, null, null, 1, 20, default)).Items);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ReviewAsync(request.Id, new() { Version = 2, Approve = true, Reason = "Retry" }, default));
        f.Actor.Group = AccountGroups.Operator;
        var retry = await f.Service.SubmitAsync(Proposal(dataset.Id), default);
        Assert.NotEqual(request.Id, retry.Id);
        f.Actor.Group = AccountGroups.InternalAdmin;
        await f.Service.ReviewAsync(retry.Id, new() { Version = 1, Approve = true, Reason = "Đã đối chiếu điểm" }, default);
        var published = (await f.Service.SharedAsync(null, null, null, 1, 20, default)).Items.Single();
        Assert.Single(await f.Service.HistoryAsync(published.Id, default));
    }

    [Fact]
    public async Task TC_LOC_007_Stale_proposal_cannot_overwrite_current_point_or_history()
    {
        using var f = await Fixture.Create();
        var dataset = await f.Service.ImportAsync(Dataset(), default);
        var location = await f.Service.SaveSharedAsync(null, Shared(dataset.Id), default);
        f.Actor.Group = AccountGroups.Operator;
        var proposal = Proposal(dataset.Id); proposal.Kind = "Update"; proposal.LocationId = location.Id; proposal.BaseVersion = 1;
        var row = await f.Service.SubmitAsync(proposal, default);
        f.Actor.Group = AccountGroups.InternalAdmin;
        var edit = Shared(dataset.Id); edit.Version = 1; edit.Name = "Latest";
        await f.Service.SaveSharedAsync(location.Id, edit, default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ReviewAsync(row.Id, new() { Version = 1, Approve = true, Reason = "Approve stale" }, default));
        Assert.Equal("Pending", row.Status);
        var history = await f.Service.HistoryAsync(location.Id, default);
        Assert.Equal(2, history.Count);
        Assert.Equal("Test gate", history.Single(x => x.Version == 1).Name);
        await f.Service.ArchiveSharedAsync(location.Id, new() { Version = 2, Reason = "Đóng điểm" }, default);
        Assert.Empty((await f.Service.SharedAsync(null, null, null, 1, 20, default)).Items);
        Assert.Equal(3, (await f.Service.HistoryAsync(location.Id, default)).Count);
    }

    [Fact]
    public async Task TC_LOC_008_Customer_and_owner_cannot_publish_and_admin_requires_permission()
    {
        using var f = await Fixture.Create();
        var dataset = await f.Service.ImportAsync(Dataset(), default);
        foreach (var group in new[] { AccountGroups.Customer, AccountGroups.Operator })
        {
            f.Actor.Group = group;
            await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.SaveSharedAsync(null, Shared(dataset.Id), default));
        }
        f.Actor.Group = AccountGroups.InternalAdmin; f.Actor.Manage = false;
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.ImportAsync(Dataset("v2"), default));
        f.Actor.Active = false;
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => f.Service.ProvincesAsync(default));
    }

    [Fact]
    public async Task TC_LOC_009_History_and_administrative_units_cannot_be_edited_in_place()
    {
        using var f = await Fixture.Create();
        var dataset = await f.Service.ImportAsync(Dataset(), default);
        var location = await f.Service.SaveSharedAsync(null, Shared(dataset.Id), default);
        var original = await f.Db.LocationVersions.SingleAsync();
        original.Name = "Overwrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();
        Assert.Equal("Test gate", (await f.Service.HistoryAsync(location.Id, default)).Single().Name);
        var province = await f.Db.Provinces.SingleAsync(); province.Name = "Overwrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task TC_LOC_010_Concurrent_writer_cannot_commit_a_stale_version()
    {
        using var f = await Fixture.Create();
        var dataset = await f.Service.ImportAsync(Dataset(), default);
        var location = await f.Service.SaveSharedAsync(null, Shared(dataset.Id), default);
        using var secondDb = new LocationsDbContext(new DbContextOptionsBuilder<LocationsDbContext>()
            .UseSqlite(f.Db.Database.GetDbConnection()).Options);
        await secondDb.SharedLocations.SingleAsync(); // Track version 1 before the first writer commits.
        var edit = Shared(dataset.Id); edit.Version = 1; edit.Name = "First writer";
        await f.Service.SaveSharedAsync(location.Id, edit, default);
        var secondService = new LocationsService(secondDb, f.Actor, f.Actor, new OwnerAccess());
        edit.Name = "Stale writer";
        await Assert.ThrowsAsync<ConflictException>(() => secondService.SaveSharedAsync(location.Id, edit, default));
        Assert.Equal("First writer", (await f.Service.SharedDetailAsync(location.Id, default)).Name);
        Assert.Equal(2, (await f.Service.HistoryAsync(location.Id, default)).Count);
    }

    private static ImportDatasetRequest Dataset(string version = "v1") => new()
    {
        Source = "fixture",
        Version = version,
        Checksum = new string('a', 64),
        Provinces = [new() { Code = "01", Name = "Tỉnh mẫu" }],
        Wards = [new() { Code = "00001", ProvinceCode = "01", Name = "Phường mẫu" }]
    };
    private static SaveAddressRequest Address(Guid dataset) => new() { DatasetId = dataset, ProvinceCode = "01", WardCode = "00001", AddressDetail = "Số 1, cổng chính" };
    private static SaveSharedLocationRequest Shared(Guid dataset) => new()
    {
        DatasetId = dataset,
        ProvinceCode = "01",
        WardCode = "00001",
        AddressDetail = "Main gate",
        Name = "Test gate",
        Type = "PickupPoint",
        Latitude = 21,
        Longitude = 105,
        ConfirmationMethod = "MapPin",
        Reason = "Initial publication"
    };
    private static SubmitLocationRequest Proposal(Guid dataset) => new()
    {
        OrganizationId = Guid.NewGuid(),
        Kind = "Create",
        DatasetId = dataset,
        ProvinceCode = "01",
        WardCode = "00001",
        AddressDetail = "Main gate",
        Name = "Test gate",
        Type = "PickupPoint",
        Latitude = 21,
        Longitude = 105,
        ConfirmationMethod = "ManualCoordinates",
        Reason = "Đề xuất điểm"
    };

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public LocationsDbContext Db { get; private set; } = null!;
        public Actor Actor { get; } = new();
        public LocationsService Service => new(Db, Actor, Actor, new OwnerAccess());
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); await f.connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<LocationsDbContext>().UseSqlite(f.connection).Options);
            await f.Db.Database.EnsureCreatedAsync(); return f;
        }
        public void Dispose() { Db.Dispose(); connection.Dispose(); }
    }
    private sealed class Actor : ICurrentUser, IAccountDirectory
    {
        public Guid Id = Guid.NewGuid(); public string Group = AccountGroups.InternalAdmin;
        public bool Manage = true, Active = true;
        public bool IsAuthenticated => true; public Guid? UserId => Id; public string? Email => "test@example.test";
        public IReadOnlyList<string> Roles => []; public IReadOnlyList<string> Permissions => Manage ? ["locations.manage"] : [];
        public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
        public Task<AccountSummary?> FindActiveAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<AccountSummary?>(Active ? new(accountId, Email!, Group, true) : null);
    }
    private sealed class OwnerAccess : ILocationOrganizationAccess
    {
        public Task RequireOwnerAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
