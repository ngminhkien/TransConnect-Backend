using Microsoft.EntityFrameworkCore;
using TransConnect.Data;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence;

public sealed class OperatorDbContext(DbContextOptions<OperatorDbContext> options) : AppDbContextBase(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> Members => Set<OrganizationMember>();
    public DbSet<OrganizationInvitation> Invitations => Set<OrganizationInvitation>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverEngagement> Engagements => Set<DriverEngagement>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<OrganizationVehicle> OrganizationVehicles => Set<OrganizationVehicle>();
    public DbSet<OperatorAuditLog> AuditLogs => Set<OperatorAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("operator");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperatorDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
