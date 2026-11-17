using TransConnect.Base.Entities;

namespace TransConnect.Modules.Transport.Entities;

// Tuyến xe khách: danh tính ổn định; điểm dừng nằm trong các bản (revision) bất biến sau công bố.
public sealed class TransportRoute : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ServiceRegistrationId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = RouteStatuses.Active;
    public string? StatusReason { get; set; }
    public Guid? CurrentRevisionId { get; set; }
    public int Version { get; set; } = 1;
}

public static class RouteStatuses
{
    public const string Active = "Active", Inactive = "Inactive";
}

public sealed class RouteRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RouteId { get; set; }
    public int RevisionNo { get; set; }
    public string Status { get; set; } = RevisionStatuses.Draft;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public int Version { get; set; } = 1;
}

public static class RevisionStatuses
{
    public const string Draft = "Draft", Published = "Published", Retired = "Retired";
}

public sealed class RouteRevisionStop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RevisionId { get; set; }
    public int Sequence { get; set; }
    public Guid SharedLocationId { get; set; }
    // Snapshot điểm chung tại lúc lưu/công bố; bản đã công bố không đọc lại danh mục hiện hành.
    public int LocationVersion { get; set; }
    public string Name { get; set; } = "";
    public string AddressDetail { get; set; } = "";
    public string ProvinceCode { get; set; } = "";
    public string WardCode { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool AllowPickup { get; set; }
    public bool AllowDropoff { get; set; }
    public int OffsetMinutes { get; set; }
    public int? DwellMinutes { get; set; }
}
