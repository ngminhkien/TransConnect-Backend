using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Transport;
using TransConnect.Modules.Transport.Dto;
using TransConnect.Modules.Transport.Entities;
using TransConnect.Modules.Transport.Persistence;
using TransConnect.Modules.Transport.Services;
using Xunit;

namespace TransConnect.Tests;

public sealed class TransportTests
{
    // ---------- Đăng ký dịch vụ ----------

    [Fact]
    public async Task TC_SVC_001_Unapproved_organization_cannot_register()
    {
        using var f = await Fixture.Create();
        f.Orgs.Approved = false;
        await Assert.ThrowsAsync<NotFoundException>(() => f.Services.SubmitAsync(f.OrgId, Coach(), default));
        Assert.Empty(await f.Db.Registrations.ToListAsync());
    }

    [Fact]
    public async Task TC_SVC_002_Duplicate_registration_is_rejected_and_unknown_type_is_invalid()
    {
        using var f = await Fixture.Create();
        await f.Services.SubmitAsync(f.OrgId, Coach(), default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Services.SubmitAsync(f.OrgId, Coach(), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Services.SubmitAsync(f.OrgId, new() { ServiceTypeCode = "TAXI" }, default));
    }

    [Fact]
    public async Task TC_SVC_003_Only_owner_submits()
    {
        using var f = await Fixture.Create();
        f.Orgs.Role = "Dispatcher";
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Services.SubmitAsync(f.OrgId, Coach(), default));
        f.Orgs.Role = "Manager";
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Services.SubmitAsync(f.OrgId, Coach(), default));
    }

    [Fact]
    public async Task TC_SVC_004_005_006_Review_requires_note_resubmit_keeps_id_and_version_conflicts()
    {
        using var f = await Fixture.Create();
        var reg = await f.Services.SubmitAsync(f.OrgId, Coach(), default);
        f.AsAdmin();
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Services.ReviewAsync(reg.Id, new() { Decision = "Rejected", Version = reg.Version }, default));
        var rejected = await f.Services.ReviewAsync(reg.Id, new() { Decision = "Rejected", Note = "Thiếu phạm vi", Version = reg.Version }, default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Services.ReviewAsync(reg.Id, new() { Decision = "Approved", Version = reg.Version }, default));
        f.AsOwner();
        var again = await f.Services.ResubmitAsync(f.OrgId, reg.Id, new() { ScopeNote = "Hà Nội – Lào Cai", Version = rejected.Version }, default);
        Assert.Equal(reg.Id, again.Id);
        Assert.Equal("Pending", again.Status);
        Assert.Contains(again.Events!, x => x.Action == "Resubmitted");
    }

    [Fact]
    public async Task TC_SVC_007_Suspension_pauses_active_schedules_and_blocks_new_routes()
    {
        using var f = await Fixture.Create();
        var (registration, route) = await f.ApprovedRouteAsync();
        var schedule = await f.Schedules.CreateAsync(f.OrgId, f.ScheduleRequest(route.Route.Id), default);
        schedule = await f.Schedules.ActivateAsync(f.OrgId, schedule.Id, new() { Version = schedule.Version }, default);
        f.AsAdmin();
        await f.Services.SuspendAsync(registration.Id, new() { Note = "Vi phạm", Version = registration.Version }, default);
        f.AsOwner();
        Assert.Equal("Paused", (await f.Schedules.GetAsync(f.OrgId, schedule.Id, default)).Status);
        await Assert.ThrowsAsync<ConflictException>(() => f.Routes.CreateAsync(f.OrgId, new() { Code = "R2", Name = "Khác" }, default));
    }

    [Fact]
    public async Task TC_SVC_008_Other_organization_cannot_read_registrations()
    {
        using var f = await Fixture.Create();
        await f.Services.SubmitAsync(f.OrgId, Coach(), default);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Services.ForOrganizationAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task TC_ORG_SVC_002_Intake_creates_pending_once_and_never_approves()
    {
        using var f = await Fixture.Create();
        await f.Services.CreatePendingAsync(f.OrgId, ["FIXED_ROUTE_COACH", "CHARTER", "BOGUS"], f.Actor.Id, default);
        await f.Services.CreatePendingAsync(f.OrgId, ["FIXED_ROUTE_COACH"], f.Actor.Id, default);
        var rows = await f.Db.Registrations.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.Equal("Pending", x.Status));
    }

    // ---------- Tuyến ----------

    [Fact]
    public async Task TC_ROUTE_001_Route_needs_approved_coach_service()
    {
        using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ConflictException>(() => f.Routes.CreateAsync(f.OrgId, new() { Code = "R1", Name = "Tuyến" }, default));
        await f.ApproveAsync("FREIGHT_TRUCK");
        await Assert.ThrowsAsync<ConflictException>(() => f.Routes.CreateAsync(f.OrgId, new() { Code = "R1", Name = "Tuyến" }, default));
    }

    [Fact]
    public async Task TC_ROUTE_002_Publish_validates_shape()
    {
        using var f = await Fixture.Create();
        await f.ApproveAsync("FIXED_ROUTE_COACH");
        var route = await f.Routes.CreateAsync(f.OrgId, new() { Code = "r1", Name = "Hà Nội – Lào Cai" }, default);
        Assert.Equal("R1", route.Route.Code);
        var draft = route.Draft!;
        draft = (await f.Routes.SaveDraftAsync(f.OrgId, route.Route.Id, draft.Id, Stops(draft.Version, (f.A, 0, true, false)), default)).Draft!;
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Routes.PublishAsync(f.OrgId, route.Route.Id, draft.Id, new() { Version = draft.Version }, default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Routes.SaveDraftAsync(f.OrgId, route.Route.Id, draft.Id,
            Stops(draft.Version, (f.A, 0, true, false), (f.A, 30, false, true)), default));
        draft = (await f.Routes.SaveDraftAsync(f.OrgId, route.Route.Id, draft.Id, Stops(draft.Version, (f.A, 0, true, false), (f.B, 0, false, true)), default)).Draft!;
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Routes.PublishAsync(f.OrgId, route.Route.Id, draft.Id, new() { Version = draft.Version }, default));
        draft = (await f.Routes.SaveDraftAsync(f.OrgId, route.Route.Id, draft.Id, Stops(draft.Version, (f.A, 0, false, true), (f.B, 60, false, true)), default)).Draft!;
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Routes.PublishAsync(f.OrgId, route.Route.Id, draft.Id, new() { Version = draft.Version }, default));
    }

    [Fact]
    public async Task TC_ROUTE_003_Inactive_shared_location_blocks_publish()
    {
        using var f = await Fixture.Create();
        await f.ApproveAsync("FIXED_ROUTE_COACH");
        var route = await f.Routes.CreateAsync(f.OrgId, new() { Code = "R1", Name = "Tuyến" }, default);
        var draft = (await f.Routes.SaveDraftAsync(f.OrgId, route.Route.Id, route.Draft!.Id, Stops(route.Draft.Version, (f.A, 0, true, false), (f.B, 90, false, true)), default)).Draft!;
        f.Catalog.Inactive.Add(f.B);
        var error = await Assert.ThrowsAsync<ValidationAppException>(() => f.Routes.PublishAsync(f.OrgId, route.Route.Id, draft.Id, new() { Version = draft.Version }, default));
        Assert.Contains("stops[1].sharedLocationId", error.Errors.Keys);
    }

    [Fact]
    public async Task TC_ROUTE_004_005_Published_snapshot_is_kept_and_schedule_stays_on_old_revision()
    {
        using var f = await Fixture.Create();
        var (_, route) = await f.ApprovedRouteAsync();
        var schedule = await f.Schedules.CreateAsync(f.OrgId, f.ScheduleRequest(route.Route.Id), default);
        f.Catalog.Rename(f.A, "Bến mới", 2);
        var first = (await f.Routes.GetAsync(f.OrgId, route.Route.Id, default)).Current!;
        Assert.Equal("Điểm A", first.Stops![0].Name);
        Assert.Equal(1, first.Stops[0].LocationVersion);
        var next = (await f.Routes.CreateDraftAsync(f.OrgId, route.Route.Id, default)).Draft!;
        await Assert.ThrowsAsync<ConflictException>(() => f.Routes.CreateDraftAsync(f.OrgId, route.Route.Id, default));
        var published = await f.Routes.PublishAsync(f.OrgId, route.Route.Id, next.Id, new() { Version = next.Version }, default);
        Assert.Equal(2, published.Route.CurrentRevisionNo);
        Assert.Equal("Bến mới", published.Current!.Stops![0].Name);
        Assert.Equal("Retired", published.Revisions.Single(x => x.RevisionNo == 1).Status);
        Assert.Equal(first.Id, (await f.Schedules.GetAsync(f.OrgId, schedule.Id, default)).RouteRevisionId);
        await Assert.ThrowsAsync<ConflictException>(() => f.Routes.SaveDraftAsync(f.OrgId, route.Route.Id, first.Id, Stops(first.Version), default));
    }

    // ---------- Sơ đồ ghế ----------

    [Fact]
    public async Task TC_SEAT_000_Templates_are_seeded_and_copyable()
    {
        using var f = await Fixture.Create();
        await TransportSeeder.SeedAsync(f.Db);
        await TransportSeeder.SeedAsync(f.Db);
        var templates = await f.Seats.TemplatesAsync(default);
        Assert.Equal(7, templates.Count);
        Assert.Equal(44, templates.Single(x => x.Name.Contains("45")).SellableCount);
        Assert.Equal(40, templates.Single(x => x.Name.Contains("40")).SellableCount);
        Assert.All(templates, t => Assert.Equal(t.SellableCount, t.Cells.Select(c => c.Label).Where(l => l != null).Distinct().Count()));
        var copy = await f.Seats.CreateAsync(f.OrgId, new() { SourceLayoutId = templates[0].Id, Name = "Xe 29A" }, default);
        Assert.Equal("Draft", copy.Status);
        Assert.Equal(templates[0].Cells.Count, copy.Cells.Count);
    }

    [Fact]
    public async Task TC_SEAT_001_002_Locked_layout_cannot_change_and_invalid_layouts_rejected()
    {
        using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Seats.CreateAsync(f.OrgId, Layout(("A01", 1, 1), ("A01", 1, 2)), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Seats.CreateAsync(f.OrgId, Layout(("A01", 9, 1)), default));
        f.Orgs.SeatCount = 1;
        var layout = await f.Seats.CreateAsync(f.OrgId, Layout(("A01", 1, 1), ("A02", 1, 2)), default);
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Seats.AttachAsync(f.OrgId, f.VehicleId, new() { LayoutId = layout.Id }, default));
        f.Orgs.SeatCount = 40;
        layout = await f.Seats.AttachAsync(f.OrgId, f.VehicleId, new() { LayoutId = layout.Id }, default);
        Assert.Equal("Locked", layout.Status);
        var update = Layout(("A01", 1, 1)); update.Version = layout.Version;
        await Assert.ThrowsAsync<ConflictException>(() => f.Seats.UpdateAsync(f.OrgId, layout.Id, update, default));
        await Assert.ThrowsAsync<ConflictException>(() => f.Seats.ArchiveAsync(f.OrgId, layout.Id, layout.Version, default));
    }

    [Fact]
    public async Task TC_SEAT_003_Vehicle_of_another_organization_is_not_found()
    {
        using var f = await Fixture.Create();
        var layout = await f.Seats.CreateAsync(f.OrgId, Layout(("A01", 1, 1)), default);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Seats.AttachAsync(f.OrgId, Guid.NewGuid(), new() { LayoutId = layout.Id }, default));
    }

    [Fact]
    public async Task TC_SEAT_004_Changing_vehicle_layout_cannot_drop_reserved_seats()
    {
        using var f = await Fixture.Create();
        var (_, route) = await f.ApprovedRouteAsync();
        var layout = await f.Seats.CreateAsync(f.OrgId, Layout(("A01", 1, 1), ("A02", 1, 2)), default);
        await f.Seats.AttachAsync(f.OrgId, f.VehicleId, new() { LayoutId = layout.Id }, default);
        var request = f.ScheduleRequest(route.Route.Id);
        request.PlannedVehicleId = f.VehicleId; request.VieGoSeats = ["a02"]; request.ParticipationMode = "InstantBooking";
        var schedule = await f.Schedules.CreateAsync(f.OrgId, request, default);
        Assert.Equal(["A02"], schedule.VieGoSeats);
        Assert.Equal(2, schedule.SellableSeats);
        await f.Schedules.ActivateAsync(f.OrgId, schedule.Id, new() { Version = schedule.Version }, default);
        var smaller = await f.Seats.CreateAsync(f.OrgId, Layout(("A01", 1, 1)), default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Seats.AttachAsync(f.OrgId, f.VehicleId, new() { LayoutId = smaller.Id }, default));
    }

    // ---------- Lịch chạy ----------

    [Fact]
    public async Task TC_SCH_001_Instant_booking_needs_vehicle_and_vieGo_seats()
    {
        using var f = await Fixture.Create();
        var (_, route) = await f.ApprovedRouteAsync();
        var request = f.ScheduleRequest(route.Route.Id); request.ParticipationMode = "InstantBooking";
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Schedules.CreateAsync(f.OrgId, request, default));
        request.PlannedVehicleId = f.VehicleId; request.VieGoSeats = ["A01"];
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Schedules.CreateAsync(f.OrgId, request, default));
    }

    [Fact]
    public async Task TC_SCH_002_003_Active_schedule_is_replaced_not_edited_and_duplicates_clash()
    {
        using var f = await Fixture.Create();
        var (_, route) = await f.ApprovedRouteAsync();
        var request = f.ScheduleRequest(route.Route.Id);
        var schedule = await f.Schedules.CreateAsync(f.OrgId, request, default);
        schedule = await f.Schedules.ActivateAsync(f.OrgId, schedule.Id, new() { Version = schedule.Version }, default);
        request.Version = schedule.Version;
        await Assert.ThrowsAsync<ConflictException>(() => f.Schedules.UpdateAsync(f.OrgId, schedule.Id, request, default));
        var twin = await f.Schedules.CreateAsync(f.OrgId, f.ScheduleRequest(route.Route.Id), default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Schedules.ActivateAsync(f.OrgId, twin.Id, new() { Version = twin.Version }, default));
        var from = f.Today.AddDays(10);
        var replacement = f.ScheduleRequest(route.Route.Id); replacement.DepartureTime = "21:30";
        var next = await f.Schedules.ReplaceAsync(f.OrgId, schedule.Id, new() { FromDate = from, Schedule = replacement, Version = schedule.Version }, default);
        Assert.Equal("Active", next.Status);
        Assert.Equal(schedule.Id, next.ReplacesScheduleId);
        Assert.Equal(from.AddDays(-1), (await f.Schedules.GetAsync(f.OrgId, schedule.Id, default)).EffectiveTo);
    }

    [Fact]
    public async Task TC_SCH_004_Inactive_route_blocks_activation()
    {
        using var f = await Fixture.Create();
        var (_, route) = await f.ApprovedRouteAsync();
        var schedule = await f.Schedules.CreateAsync(f.OrgId, f.ScheduleRequest(route.Route.Id), default);
        await f.Routes.DeactivateAsync(f.OrgId, route.Route.Id, new() { Version = route.Route.Version }, default);
        await Assert.ThrowsAsync<ConflictException>(() => f.Schedules.ActivateAsync(f.OrgId, schedule.Id, new() { Version = schedule.Version }, default));
    }

    // ---------- Bảng giá theo chặng (Q-M2-FARE) ----------

    [Fact]
    public async Task TC_FARE_001_Only_owner_writes_and_members_and_admin_read()
    {
        using var f = await Fixture.Create();
        var (_, route) = await f.ApprovedRouteAsync(withFare: false);
        var revision = new CreateFareRequest { RouteRevisionId = route.Current!.Id };
        f.Orgs.Role = "OrganizationManager";
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Fares.CreateAsync(f.OrgId, route.Route.Id, revision, default));
        f.Orgs.Role = "OrganizationOwner";
        var fare = await f.Fares.CreateAsync(f.OrgId, route.Route.Id, revision, default);
        Assert.Equal("Draft", fare.Status);
        Assert.Equal(1, fare.PairCount);
        await Assert.ThrowsAsync<ConflictException>(() => f.Fares.CreateAsync(f.OrgId, route.Route.Id, revision, default));
        f.Orgs.Role = "OrganizationDispatcher";
        Assert.Single(await f.Fares.ListAsync(f.OrgId, route.Route.Id, default));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Fares.DeleteDraftAsync(f.OrgId, route.Route.Id, fare.Id, fare.Version, default));
        f.AsAdmin();
        Assert.Equal(fare.Id, (await f.Fares.GetAsync(f.OrgId, route.Route.Id, fare.Id, default)).Id);
    }

    [Fact]
    public async Task TC_FARE_002_Prices_validate_pairs_amounts_and_first_to_last()
    {
        using var f = await Fixture.Create();
        var route = await f.ThreeStopRouteAsync();
        var fare = await f.Fares.CreateAsync(f.OrgId, route.Route.Id, new() { RouteRevisionId = route.Current!.Id }, default);
        Assert.Equal(3, fare.PairCount);
        SaveFareRequest Request(params (Guid From, Guid To, int Price)[] prices) => new()
        {
            EffectiveFrom = f.Today, Version = fare.Version,
            Prices = prices.Select(x => new FarePriceRequest { FromLocationId = x.From, ToLocationId = x.To, Price = x.Price }).ToList()
        };
        // Ngược chiều, không phải bội số 1.000 đ, giá 0.
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Fares.SaveAsync(f.OrgId, route.Route.Id, fare.Id, Request((f.B, f.A, 100_000)), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Fares.SaveAsync(f.OrgId, route.Route.Id, fare.Id, Request((f.A, f.B, 100_500)), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Fares.SaveAsync(f.OrgId, route.Route.Id, fare.Id, Request((f.A, f.C, 0)), default));
        // Thiếu giá điểm đầu → điểm cuối thì không công bố được (003A).
        fare = await f.Fares.SaveAsync(f.OrgId, route.Route.Id, fare.Id, Request((f.A, f.B, 100_000)), default);
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Fares.PublishAsync(f.OrgId, route.Route.Id, fare.Id, new() { Version = fare.Version }, default));
        // Chặng dài rẻ hơn chặng con chỉ cảnh báo (009A).
        fare = await f.Fares.SaveAsync(f.OrgId, route.Route.Id, fare.Id, Request((f.A, f.C, 80_000), (f.A, f.B, 100_000)), default);
        Assert.Single(fare.Warnings!);
        fare = await f.Fares.PublishAsync(f.OrgId, route.Route.Id, fare.Id, new() { Version = fare.Version }, default);
        Assert.Equal("Published", fare.Status);
        Assert.Equal("Current", fare.Phase);
        Assert.Equal(100_000, fare.Prices!.Single(x => x.ToLocationId == f.B).VipTotal);
        await Assert.ThrowsAsync<ConflictException>(() => f.Fares.SaveAsync(f.OrgId, route.Route.Id, fare.Id, Request((f.A, f.C, 80_000)), default));
    }

    [Fact]
    public async Task TC_FARE_003_Future_table_keeps_current_and_new_revision_copies_surviving_pairs()
    {
        using var f = await Fixture.Create();
        var route = await f.ThreeStopRouteAsync();
        var first = await f.PublishFareAsync(route, (f.A, f.B, 150_000), (f.A, f.C, 300_000), (f.B, f.C, 200_000));
        Assert.Equal(200_000, first.Prices!.Single(x => x.FromLocationId == f.A && x.ToLocationId == f.B).VipTotal);
        var second = await f.Fares.CreateAsync(f.OrgId, route.Route.Id, new() { RouteRevisionId = route.Current!.Id }, default);
        Assert.Equal(3, second.PriceCount);
        second = await f.Fares.SaveAsync(f.OrgId, route.Route.Id, second.Id, new()
        {
            EffectiveFrom = f.Today.AddDays(30), Version = second.Version,
            Prices = second.Prices!.Select(x => new FarePriceRequest { FromLocationId = x.FromLocationId, ToLocationId = x.ToLocationId, Price = x.Price + 100_000 }).ToList()
        }, default);
        second = await f.Fares.PublishAsync(f.OrgId, route.Route.Id, second.Id, new() { Version = second.Version }, default);
        Assert.Equal("Upcoming", second.Phase);
        Assert.Equal("Current", (await f.Fares.ListAsync(f.OrgId, route.Route.Id, default)).Single(x => x.Id == first.Id).Phase);
        // Bản tuyến mới bỏ điểm B: chỉ chép cặp A → C còn tồn tại, lấy từ bảng công bố mới nhất.
        var draft = (await f.Routes.CreateDraftAsync(f.OrgId, route.Route.Id, default)).Draft!;
        draft = (await f.Routes.SaveDraftAsync(f.OrgId, route.Route.Id, draft.Id, Stops(draft.Version, (f.A, 0, true, false), (f.C, 400, false, true)), default)).Draft!;
        var next = await f.Routes.PublishAsync(f.OrgId, route.Route.Id, draft.Id, new() { Version = draft.Version }, default);
        var copied = await f.Fares.CreateAsync(f.OrgId, route.Route.Id, new() { RouteRevisionId = next.Current!.Id }, default);
        Assert.Equal(400_000, Assert.Single(copied.Prices!).Price);
        Assert.Equal(1, copied.PairCount);
    }

    [Fact]
    public async Task TC_FARE_004_Booking_schedule_needs_published_fare_but_information_only_does_not()
    {
        using var f = await Fixture.Create();
        var (_, route) = await f.ApprovedRouteAsync(withFare: false);
        var schedule = await f.Schedules.CreateAsync(f.OrgId, f.ScheduleRequest(route.Route.Id), default);
        Assert.False(schedule.FareReady);
        await Assert.ThrowsAsync<ConflictException>(() => f.Schedules.ActivateAsync(f.OrgId, schedule.Id, new() { Version = schedule.Version }, default));
        var info = f.ScheduleRequest(route.Route.Id); info.ParticipationMode = "InformationOnly"; info.DepartureTime = "09:00";
        var infoSchedule = await f.Schedules.CreateAsync(f.OrgId, info, default);
        Assert.Equal("Active", (await f.Schedules.ActivateAsync(f.OrgId, infoSchedule.Id, new() { Version = infoSchedule.Version }, default)).Status);
        await f.PublishFareAsync(route, (f.A, f.B, 300_000));
        Assert.Equal("Active", (await f.Schedules.ActivateAsync(f.OrgId, schedule.Id, new() { Version = schedule.Version }, default)).Status);
    }

    // ---------- Helpers ----------

    private static SubmitRegistrationRequest Coach() => new() { ServiceTypeCode = "FIXED_ROUTE_COACH", ScopeNote = "Hà Nội – Lào Cai" };

    private static SaveDraftRequest Stops(int version, params (Guid Id, int Offset, bool Pickup, bool Dropoff)[] stops) => new()
    {
        Version = version,
        Stops = stops.Select(x => new StopRequest { SharedLocationId = x.Id, OffsetMinutes = x.Offset, AllowPickup = x.Pickup, AllowDropoff = x.Dropoff }).ToList()
    };

    private static SaveSeatLayoutRequest Layout(params (string Label, int Row, int Col)[] seats) => new()
    {
        Name = "Sơ đồ thử", LayoutKind = "Seater",
        Decks = [new() { DeckNo = 1, Rows = 3, Columns = 4 }],
        Cells = seats.Select(x => new CellRequest { DeckNo = 1, Row = x.Row, Col = x.Col, CellType = "Seat", Label = x.Label }).ToList()
    };

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public TransportDbContext Db { get; private set; } = null!;
        public Actor Actor { get; } = new();
        public Orgs Orgs { get; } = new();
        public Catalog Catalog { get; } = new();
        public Guid OrgId => Orgs.OrgId;
        public Guid VehicleId => Orgs.VehicleId;
        public Guid A => Catalog.A;
        public Guid B => Catalog.B;
        private readonly TimeProvider clock = TimeProvider.System;
        private TransportAccess Access => new(Actor, Actor, Orgs, clock);
        public DateOnly Today => Access.Today;
        public ServiceRegistrationService Services => new(Db, Access);
        public RouteService Routes => new(Db, Access, Catalog);
        public SeatLayoutService Seats => new(Db, Access);
        public ScheduleService Schedules => new(Db, Access, Seats);
        public FareService Fares => new(Db, Access);
        public Guid C => Catalog.C;

        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); await f.connection.OpenAsync();
            f.Db = new(new DbContextOptionsBuilder<TransportDbContext>().UseSqlite(f.connection).Options);
            await f.Db.Database.EnsureCreatedAsync();
            return f;
        }

        public void AsAdmin() => Actor.Group = AccountGroups.InternalAdmin;
        public void AsOwner() => Actor.Group = AccountGroups.Operator;

        public async Task<RegistrationResponse> ApproveAsync(string code)
        {
            AsOwner();
            var reg = await Services.SubmitAsync(OrgId, new() { ServiceTypeCode = code }, default);
            AsAdmin();
            reg = await Services.ReviewAsync(reg.Id, new() { Decision = "Approved", Version = reg.Version }, default);
            AsOwner();
            return reg;
        }

        public async Task<(RegistrationResponse, RouteDetailResponse)> ApprovedRouteAsync(bool withFare = true)
        {
            var reg = await ApproveAsync("FIXED_ROUTE_COACH");
            var route = await Routes.CreateAsync(OrgId, new() { Code = "R1", Name = "Hà Nội – Lào Cai" }, default);
            var draft = (await Routes.SaveDraftAsync(OrgId, route.Route.Id, route.Draft!.Id,
                Stops(route.Draft.Version, (A, 0, true, false), (B, 300, false, true)), default)).Draft!;
            route = await Routes.PublishAsync(OrgId, route.Route.Id, draft.Id, new() { Version = draft.Version }, default);
            if (withFare) await PublishFareAsync(route, (A, B, 300_000));
            return (reg, route);
        }

        public async Task<FareTableResponse> PublishFareAsync(RouteDetailResponse route, params (Guid From, Guid To, int Price)[] prices)
        {
            var fare = await Fares.CreateAsync(OrgId, route.Route.Id, new() { RouteRevisionId = route.Current!.Id }, default);
            fare = await Fares.SaveAsync(OrgId, route.Route.Id, fare.Id, new()
            {
                EffectiveFrom = Today, VipSurcharge = 50_000, Version = fare.Version,
                Prices = prices.Select(x => new FarePriceRequest { FromLocationId = x.From, ToLocationId = x.To, Price = x.Price }).ToList()
            }, default);
            return await Fares.PublishAsync(OrgId, route.Route.Id, fare.Id, new() { Version = fare.Version }, default);
        }

        public async Task<RouteDetailResponse> ThreeStopRouteAsync()
        {
            await ApproveAsync("FIXED_ROUTE_COACH");
            var route = await Routes.CreateAsync(OrgId, new() { Code = "R3", Name = "A – B – C" }, default);
            var draft = (await Routes.SaveDraftAsync(OrgId, route.Route.Id, route.Draft!.Id,
                Stops(route.Draft.Version, (A, 0, true, false), (B, 120, true, true), (C, 300, false, true)), default)).Draft!;
            return await Routes.PublishAsync(OrgId, route.Route.Id, draft.Id, new() { Version = draft.Version }, default);
        }

        public SaveScheduleRequest ScheduleRequest(Guid routeId) => new()
        {
            RouteId = routeId, DepartureTime = "07:00", DaysOfWeek = [1, 3, 5], EffectiveFrom = Today,
            ParticipationMode = "RequestConfirmation"
        };

        public void Dispose() { Db.Dispose(); connection.Dispose(); }
    }

    private sealed class Actor : ICurrentUser, IAccountDirectory
    {
        public Guid Id = Guid.NewGuid();
        public string Group = AccountGroups.Operator;
        public bool IsAuthenticated => true;
        public Guid? UserId => Id;
        public string? Email => "transport@example.test";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => Group == AccountGroups.InternalAdmin ? [TransportAccess.ReviewPermission] : [];
        public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
        public Task<AccountSummary?> FindActiveAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<AccountSummary?>(new(accountId, Email!, Group, true));
    }

    private sealed class Orgs : ITransportOrganizationAccess
    {
        public Guid OrgId = Guid.NewGuid(), VehicleId = Guid.NewGuid();
        public bool Approved = true;
        public string Role = "OrganizationOwner";
        public int? SeatCount = 45;

        public Task<TransportMembership> RequireMemberAsync(Guid organizationId, Guid accountId, CancellationToken ct = default)
            => organizationId == OrgId && Approved
                ? Task.FromResult(new TransportMembership(organizationId, accountId, Role))
                : throw new NotFoundException("Không tìm thấy tổ chức.");

        public async Task<TransportOrganization?> FindAsync(Guid organizationId, CancellationToken ct = default)
            => (await FindManyAsync([organizationId], ct)).GetValueOrDefault(organizationId);

        public Task<IReadOnlyDictionary<Guid, TransportOrganization>> FindManyAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, TransportOrganization>>(organizationIds.Where(x => x == OrgId)
                .ToDictionary(x => x, x => new TransportOrganization(x, "Nhà xe thử", "0123456789", Approved ? "Approved" : "Pending", null, [])));

        public Task<TransportVehicle?> FindManagedVehicleAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
            => Task.FromResult(organizationId == OrgId && vehicleId == VehicleId
                ? new TransportVehicle(vehicleId, organizationId, "29B-123.45", "Coach", SeatCount) : null);
    }

    private sealed class Catalog : ISharedLocationCatalog
    {
        public Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();
        public HashSet<Guid> Inactive = [];
        private readonly Dictionary<Guid, (string Name, int Version)> names = [];

        public Catalog()
        {
            names[A] = ("Điểm A", 1);
            names[B] = ("Điểm B", 1);
            names[C] = ("Điểm C", 1);
        }

        public void Rename(Guid id, string name, int version) => names[id] = (name, version);

        public Task<IReadOnlyDictionary<Guid, SharedLocationSnapshot>> GetActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, SharedLocationSnapshot>>(ids.Where(x => names.ContainsKey(x) && !Inactive.Contains(x))
                .Distinct().ToDictionary(x => x, x => new SharedLocationSnapshot(x, names[x].Version, names[x].Name, "BusStation",
                    "Số 1", "01", "00001", 21m, 105m)));
    }
}
