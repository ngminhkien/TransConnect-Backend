using TransConnect.Base.Entities;

namespace TransConnect.Modules.Transport.Entities;

// Sơ đồ ghế: OrganizationId null = mẫu hệ thống. Khóa khi gắn vào xe (Q-M2-SERVICE-005b).
public sealed class SeatLayout : BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string LayoutKind { get; set; } = SeatLayoutKinds.Seater;
    public int DeckCount { get; set; } = 1;
    public string Status { get; set; } = SeatLayoutStatuses.Draft;
    public Guid? SourceLayoutId { get; set; }
    public int Version { get; set; } = 1;
}

public static class SeatLayoutKinds
{
    public const string Seater = "Seater", Sleeper = "Sleeper", Cabin = "Cabin";
    public static readonly string[] All = [Seater, Sleeper, Cabin];
}

public static class SeatLayoutStatuses
{
    public const string Draft = "Draft", Locked = "Locked", Archived = "Archived";
}

public sealed class SeatLayoutDeck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LayoutId { get; set; }
    public int DeckNo { get; set; }
    public int Rows { get; set; }
    public int Columns { get; set; }
}

public sealed class SeatLayoutCell
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LayoutId { get; set; }
    public int DeckNo { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public string CellType { get; set; } = SeatCellTypes.Empty;
    public string? Label { get; set; }
    public string? SeatClass { get; set; }
    public bool IsSellable { get; set; }
}

public static class SeatCellTypes
{
    public const string Seat = "Seat", Aisle = "Aisle", Empty = "Empty", Driver = "Driver", Door = "Door";
    public static readonly string[] All = [Seat, Aisle, Empty, Driver, Door];
}

public static class SeatClasses
{
    public const string Standard = "Standard", Vip = "Vip";
    public static readonly string[] All = [Standard, Vip];
}

public sealed class VehicleSeatLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid LayoutId { get; set; }
    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
    public Guid AttachedBy { get; set; }
    public DateTime? EndedAt { get; set; }
}
