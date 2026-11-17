using Microsoft.EntityFrameworkCore;
using TransConnect.Data;
using TransConnect.Modules.Transport.Entities;

namespace TransConnect.Modules.Transport.Persistence;

public sealed class TransportDbContext(DbContextOptions<TransportDbContext> options) : AppDbContextBase(options)
{
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<ServiceRegistration> Registrations => Set<ServiceRegistration>();
    public DbSet<ServiceRegistrationEvent> RegistrationEvents => Set<ServiceRegistrationEvent>();
    public DbSet<TransportRoute> Routes => Set<TransportRoute>();
    public DbSet<RouteRevision> Revisions => Set<RouteRevision>();
    public DbSet<RouteRevisionStop> Stops => Set<RouteRevisionStop>();
    public DbSet<SeatLayout> SeatLayouts => Set<SeatLayout>();
    public DbSet<SeatLayoutDeck> SeatDecks => Set<SeatLayoutDeck>();
    public DbSet<SeatLayoutCell> SeatCells => Set<SeatLayoutCell>();
    public DbSet<VehicleSeatLayout> VehicleLayouts => Set<VehicleSeatLayout>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleVieGoSeat> VieGoSeats => Set<ScheduleVieGoSeat>();
    public DbSet<FareTable> FareTables => Set<FareTable>();
    public DbSet<FarePrice> FarePrices => Set<FarePrice>();

    private void ValidateImmutableHistory()
    {
        if (ChangeTracker.Entries<ServiceRegistrationEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Service registration history is immutable.");
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
        model.HasDefaultSchema("transport");

        model.Entity<ServiceType>(b =>
        {
            b.ToTable("ServiceTypes");
            b.HasKey(x => x.Code);
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            b.Property(x => x.AllowedModes).HasMaxLength(200).IsRequired();
            b.HasData(
                new ServiceType
                {
                    Code = "FIXED_ROUTE_COACH", Name = "Xe khách tuyến cố định", SortOrder = 1, IsOperational = true,
                    Description = "Chở khách theo tuyến, điểm dừng và lịch chạy đã công bố.",
                    AllowedModes = "InformationOnly,RequestConfirmation,InstantBooking"
                },
                new ServiceType
                {
                    Code = "FREIGHT_TRUCK", Name = "Xe tải chở hàng / ghép hàng", SortOrder = 2, IsOperational = false,
                    Description = "Đăng chuyến theo ngày, nhận hàng lẻ hoặc nguyên chuyến. Chưa mở vận hành.",
                    AllowedModes = "InformationOnly,RequestConfirmation"
                },
                new ServiceType
                {
                    Code = "CHARTER", Name = "Thuê nguyên xe / xe hợp đồng", SortOrder = 3, IsOperational = false,
                    Description = "Khách gửi yêu cầu, nhà xe báo giá, hai bên xác nhận. Chưa mở vận hành.",
                    AllowedModes = "InformationOnly,RequestConfirmation"
                });
        });

        model.Entity<ServiceRegistration>(b =>
        {
            b.ToTable("ServiceRegistrations", t => t.HasCheckConstraint("CK_ServiceRegistrations_Status",
                "[Status] IN ('Pending', 'Approved', 'Rejected', 'Suspended', 'Withdrawn')"));
            b.Property(x => x.ServiceTypeCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.ScopeNote).HasMaxLength(2000);
            b.Property(x => x.ReviewNote).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<ServiceType>().WithMany().HasForeignKey(x => x.ServiceTypeCode).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrganizationId, x.ServiceTypeCode }).IsUnique()
                .HasFilter("[Status] <> 'Withdrawn' AND [DeletedAt] IS NULL");
            b.HasIndex(x => new { x.Status, x.ServiceTypeCode });
        });

        model.Entity<ServiceRegistrationEvent>(b =>
        {
            b.ToTable("ServiceRegistrationEvents");
            b.Property(x => x.Action).HasMaxLength(30).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.Note).HasMaxLength(2000);
            b.Property(x => x.ScopeNote).HasMaxLength(2000);
            b.HasOne<ServiceRegistration>().WithMany().HasForeignKey(x => x.RegistrationId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RegistrationId, x.CreatedAt });
        });

        model.Entity<TransportRoute>(b =>
        {
            b.ToTable("Routes", t => t.HasCheckConstraint("CK_Routes_Status", "[Status] IN ('Active', 'Inactive')"));
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(255).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.StatusReason).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<ServiceRegistration>().WithMany().HasForeignKey(x => x.ServiceRegistrationId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique().HasFilter("[DeletedAt] IS NULL");
        });

        model.Entity<RouteRevision>(b =>
        {
            b.ToTable("RouteRevisions", t => t.HasCheckConstraint("CK_RouteRevisions_Status", "[Status] IN ('Draft', 'Published', 'Retired')"));
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TransportRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RouteId, x.RevisionNo }).IsUnique();
            b.HasIndex(x => x.RouteId).IsUnique().HasFilter("[Status] = 'Draft'").HasDatabaseName("IX_RouteRevisions_OneDraft");
        });

        model.Entity<RouteRevisionStop>(b =>
        {
            b.ToTable("RouteRevisionStops", t => t.HasCheckConstraint("CK_RouteRevisionStops_Offsets",
                "[Sequence] >= 1 AND [OffsetMinutes] >= 0 AND ([DwellMinutes] IS NULL OR [DwellMinutes] >= 0)"));
            b.Property(x => x.Name).HasMaxLength(255).IsRequired();
            b.Property(x => x.AddressDetail).HasMaxLength(500).IsRequired();
            b.Property(x => x.ProvinceCode).HasMaxLength(20).IsRequired();
            b.Property(x => x.WardCode).HasMaxLength(20).IsRequired();
            b.Property(x => x.Latitude).HasPrecision(10, 7);
            b.Property(x => x.Longitude).HasPrecision(10, 7);
            b.HasOne<RouteRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.RevisionId, x.Sequence }).IsUnique();
            b.HasIndex(x => new { x.RevisionId, x.SharedLocationId }).IsUnique();
        });

        model.Entity<SeatLayout>(b =>
        {
            b.ToTable("SeatLayouts", t =>
            {
                t.HasCheckConstraint("CK_SeatLayouts_Status", "[Status] IN ('Draft', 'Locked', 'Archived')");
                t.HasCheckConstraint("CK_SeatLayouts_Kind", "[LayoutKind] IN ('Seater', 'Sleeper', 'Cabin')");
                t.HasCheckConstraint("CK_SeatLayouts_Decks", "[DeckCount] BETWEEN 1 AND 2");
            });
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.LayoutKind).HasMaxLength(20).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.OrganizationId);
        });

        model.Entity<SeatLayoutDeck>(b =>
        {
            b.ToTable("SeatLayoutDecks", t => t.HasCheckConstraint("CK_SeatLayoutDecks_Size",
                "[Rows] BETWEEN 1 AND 20 AND [Columns] BETWEEN 1 AND 6"));
            b.HasOne<SeatLayout>().WithMany().HasForeignKey(x => x.LayoutId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.LayoutId, x.DeckNo }).IsUnique();
        });

        model.Entity<SeatLayoutCell>(b =>
        {
            b.ToTable("SeatLayoutCells", t => t.HasCheckConstraint("CK_SeatLayoutCells_Type",
                "[CellType] IN ('Seat', 'Aisle', 'Empty', 'Driver', 'Door')"));
            b.Property(x => x.CellType).HasMaxLength(20).IsRequired();
            b.Property(x => x.Label).HasMaxLength(10);
            b.Property(x => x.SeatClass).HasMaxLength(20);
            b.HasOne<SeatLayout>().WithMany().HasForeignKey(x => x.LayoutId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.LayoutId, x.DeckNo, x.Row, x.Col }).IsUnique();
            b.HasIndex(x => new { x.LayoutId, x.Label }).IsUnique().HasFilter("[Label] IS NOT NULL");
        });

        model.Entity<VehicleSeatLayout>(b =>
        {
            b.ToTable("VehicleSeatLayouts");
            b.HasOne<SeatLayout>().WithMany().HasForeignKey(x => x.LayoutId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrganizationId, x.VehicleId }).IsUnique().HasFilter("[EndedAt] IS NULL");
        });

        model.Entity<Schedule>(b =>
        {
            b.ToTable("Schedules", t =>
            {
                t.HasCheckConstraint("CK_Schedules_Status", "[Status] IN ('Draft', 'Active', 'Paused', 'Ended')");
                t.HasCheckConstraint("CK_Schedules_Mode", "[ParticipationMode] IN ('InformationOnly', 'RequestConfirmation', 'InstantBooking')");
                t.HasCheckConstraint("CK_Schedules_Time", "[DepartureMinutes] BETWEEN 0 AND 1439 AND [DaysOfWeek] BETWEEN 1 AND 127");
                t.HasCheckConstraint("CK_Schedules_Range", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            });
            b.Property(x => x.ParticipationMode).HasMaxLength(30).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.StatusReason).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TransportRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RouteRevision>().WithMany().HasForeignKey(x => x.RouteRevisionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrganizationId, x.RouteId, x.Status });
            b.HasIndex(x => new { x.OrganizationId, x.PlannedVehicleId });
        });

        model.Entity<ScheduleVieGoSeat>(b =>
        {
            b.ToTable("ScheduleVieGoSeats");
            b.Property(x => x.SeatLabel).HasMaxLength(10).IsRequired();
            b.HasOne<Schedule>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ScheduleId, x.SeatLabel }).IsUnique();
        });

        model.Entity<FareTable>(b =>
        {
            b.ToTable("FareTables", t =>
            {
                t.HasCheckConstraint("CK_FareTables_Status", "[Status] IN ('Draft', 'Published', 'Superseded')");
                t.HasCheckConstraint("CK_FareTables_Vip", "[VipSurcharge] >= 0");
            });
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TransportRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RouteRevision>().WithMany().HasForeignKey(x => x.RouteRevisionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RouteRevisionId, x.TableNo }).IsUnique();
            b.HasIndex(x => x.RouteRevisionId).IsUnique().HasFilter("[Status] = 'Draft'").HasDatabaseName("IX_FareTables_OneDraft");
            b.HasIndex(x => new { x.RouteId, x.Status });
        });

        model.Entity<FarePrice>(b =>
        {
            b.ToTable("FarePrices", t => t.HasCheckConstraint("CK_FarePrices_Amount",
                "[Price] > 0 AND ([VipPrice] IS NULL OR [VipPrice] > 0)"));
            b.HasOne<FareTable>().WithMany().HasForeignKey(x => x.FareTableId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.FareTableId, x.FromLocationId, x.ToLocationId }).IsUnique();
        });

        base.OnModelCreating(model);
    }
}
