namespace TransConnect.Modules.Transport.Entities;

// Bảng giá theo chặng của một bản tuyến (Q-M2-FARE-001A). Có phiên bản: nháp → công bố áp dụng từ ngày (005A).
public sealed class FareTable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid RouteId { get; set; }
    public Guid RouteRevisionId { get; set; }
    // Số thứ tự bảng giá trong cùng bản tuyến.
    public int TableNo { get; set; }
    public string Status { get; set; } = FareStatuses.Draft;
    public DateOnly EffectiveFrom { get; set; }
    // Phụ thu ghế Vip cố định cho cả bảng, VNĐ (004A).
    public int VipSurcharge { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public int Version { get; set; } = 1;
}

public static class FareStatuses
{
    // Superseded: bảng đã công bố nhưng bị bảng công bố sau có ngày áp dụng sớm hơn hoặc bằng thay thế trước khi có hiệu lực.
    public const string Draft = "Draft", Published = "Published", Superseded = "Superseded";
}

// Giá một cặp điểm đón → điểm trả, khóa theo điểm chung VieGo để sao chép được sang bản tuyến mới.
public sealed class FarePrice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FareTableId { get; set; }
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public int Price { get; set; }
    // Ghi đè giá ghế Vip cho cặp này; trống = Price + VipSurcharge.
    public int? VipPrice { get; set; }
}
