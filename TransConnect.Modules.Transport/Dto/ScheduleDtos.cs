namespace TransConnect.Modules.Transport.Dto;

public sealed class SaveScheduleRequest
{
    public Guid RouteId { get; set; }
    // Trống = bản đang công bố của tuyến.
    public Guid? RouteRevisionId { get; set; }
    // "HH:mm", giờ Việt Nam.
    public string DepartureTime { get; set; } = "";
    // 1 = Thứ 2 ... 7 = Chủ nhật.
    public List<int> DaysOfWeek { get; set; } = [];
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public Guid? PlannedVehicleId { get; set; }
    public string ParticipationMode { get; set; } = "";
    public List<string> VieGoSeats { get; set; } = [];
    public int Version { get; set; }
}

public sealed class ReplaceScheduleRequest
{
    // Lịch mới bắt đầu từ ngày này; lịch cũ kết thúc ngày trước đó.
    public DateOnly FromDate { get; set; }
    public SaveScheduleRequest Schedule { get; set; } = new();
    public int Version { get; set; }
}

public sealed class ScheduleStatusRequest
{
    public string? Reason { get; set; }
    public int Version { get; set; }
}

public sealed record ScheduleResponse(Guid Id, Guid RouteId, string RouteCode, string RouteName, Guid RouteRevisionId,
    int RevisionNo, string DepartureTime, IReadOnlyList<int> DaysOfWeek, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    Guid? PlannedVehicleId, string? PlannedVehiclePlate, int? SellableSeats, string ParticipationMode,
    IReadOnlyList<string> VieGoSeats, string Status, string? StatusReason, Guid? ReplacesScheduleId, int Version,
    // false khi lịch nhận khách qua VieGo nhưng chưa có bảng giá công bố áp dụng (010A).
    bool FareReady = true);
