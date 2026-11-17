namespace TransConnect.Modules.Transport.Dto;

public sealed class DeckRequest
{
    public int DeckNo { get; set; }
    public int Rows { get; set; }
    public int Columns { get; set; }
}

public sealed class CellRequest
{
    public int DeckNo { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public string CellType { get; set; } = "";
    public string? Label { get; set; }
    public string? SeatClass { get; set; }
    public bool IsSellable { get; set; } = true;
}

public sealed class SaveSeatLayoutRequest
{
    public string Name { get; set; } = "";
    public string LayoutKind { get; set; } = "";
    // Tạo bằng cách sao chép: bỏ qua Decks/Cells nếu có SourceLayoutId.
    public Guid? SourceLayoutId { get; set; }
    public List<DeckRequest> Decks { get; set; } = [];
    public List<CellRequest> Cells { get; set; } = [];
    public int Version { get; set; }
}

public sealed class AttachSeatLayoutRequest
{
    public Guid LayoutId { get; set; }
}

public sealed record DeckResponse(int DeckNo, int Rows, int Columns);

public sealed record CellResponse(int DeckNo, int Row, int Col, string CellType, string? Label, string? SeatClass, bool IsSellable);

public sealed record SeatLayoutResponse(Guid Id, Guid? OrganizationId, string Name, string LayoutKind, int DeckCount,
    string Status, Guid? SourceLayoutId, int Version, int SeatCount, int SellableCount,
    IReadOnlyList<DeckResponse> Decks, IReadOnlyList<CellResponse> Cells, IReadOnlyList<Guid> VehicleIds);

public sealed record VehicleSeatLayoutResponse(Guid VehicleId, Guid LayoutId, string LayoutName, int SellableCount, DateTime AttachedAt);
