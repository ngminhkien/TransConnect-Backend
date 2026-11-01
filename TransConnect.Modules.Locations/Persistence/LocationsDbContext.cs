using Microsoft.EntityFrameworkCore;
using TransConnect.Data;
using TransConnect.Modules.Locations.Entities;
namespace TransConnect.Modules.Locations.Persistence;

public sealed class LocationsDbContext(DbContextOptions<LocationsDbContext> options) : AppDbContextBase(options)
{
    public DbSet<AdministrativeDataset> Datasets => Set<AdministrativeDataset>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<Ward> Wards => Set<Ward>();
    public DbSet<CustomerAddress> Addresses => Set<CustomerAddress>();
    public DbSet<SharedLocation> SharedLocations => Set<SharedLocation>();
    public DbSet<SharedLocationVersion> LocationVersions => Set<SharedLocationVersion>();
    public DbSet<LocationChangeRequest> Requests => Set<LocationChangeRequest>();
    private void ValidateImmutableHistory()
    {
        if (ChangeTracker.Entries<SharedLocationVersion>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<Province>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<Ward>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Published history and administrative units are immutable. Create a new version instead.");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateImmutableHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateImmutableHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("locations");
        model.ApplyConfigurationsFromAssembly(typeof(LocationsDbContext).Assembly);
        base.OnModelCreating(model);
    }
}
