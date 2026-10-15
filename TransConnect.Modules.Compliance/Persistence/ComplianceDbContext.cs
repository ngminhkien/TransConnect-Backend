using Microsoft.EntityFrameworkCore;
using TransConnect.Data;
using TransConnect.Modules.Compliance.Entities;

namespace TransConnect.Modules.Compliance.Persistence;

public sealed class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : AppDbContextBase(options)
{
    public DbSet<DemoDocument> DemoDocuments => Set<DemoDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("compliance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ComplianceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
