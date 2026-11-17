using TransConnect.Base.Entities;

namespace TransConnect.Modules.Transport.Entities;

// Lịch chạy lặp theo tuần. Lịch Active không sửa đè; thay bằng lịch mới (Q-07).
public sealed class Schedule : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid RouteId { get; set; }
    public Guid RouteRevisionId { get; set; }
    // Giờ xuất bến tính bằng phút từ 00:00 giờ Việt Nam.
    public int DepartureMinutes { get; set; }
    // Bit 0 = Thứ 2 ... bit 6 = Chủ nhật.
    public int DaysOfWeek { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public Guid? PlannedVehicleId { get; set; }
    public string ParticipationMode { get; set; } = ParticipationModes.RequestConfirmation;
    public string Status { get; set; } = ScheduleStatuses.Draft;
    public string? StatusReason { get; set; }
    public Guid? ReplacesScheduleId { get; set; }
    public int Version { get; set; } = 1;
}

public static class ScheduleStatuses
{
    public const string Draft = "Draft", Active = "Active", Paused = "Paused", Ended = "Ended";
}

public static class ParticipationModes
{
    public const string InformationOnly = "InformationOnly", RequestConfirmation = "RequestConfirmation", InstantBooking = "InstantBooking";
    public static readonly string[] All = [InformationOnly, RequestConfirmation, InstantBooking];
}

public sealed class ScheduleVieGoSeat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScheduleId { get; set; }
    public string SeatLabel { get; set; } = "";
}
