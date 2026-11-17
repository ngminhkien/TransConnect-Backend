using Microsoft.EntityFrameworkCore;
using TransConnect.Modules.Transport.Entities;

namespace TransConnect.Modules.Transport.Persistence;

// Mẫu sơ đồ ghế hệ thống (Q-M2-SERVICE-005a). Nhà xe sao chép rồi sửa cho khớp xe thật.
// Idempotent theo tên mẫu; không sửa mẫu đã có.
public static class TransportSeeder
{
    private sealed record Template(string Name, string Kind, int Decks, int Columns, int[] AisleColumns, int SeatsPerDeck);

    private static readonly Template[] Templates =
    [
        new("Mẫu ghế ngồi 16 chỗ", SeatLayoutKinds.Seater, 1, 4, [3], 15),
        new("Mẫu ghế ngồi 29 chỗ", SeatLayoutKinds.Seater, 1, 5, [3], 28),
        new("Mẫu ghế ngồi 45 chỗ", SeatLayoutKinds.Seater, 1, 5, [3], 44),
        new("Mẫu giường nằm 34 giường", SeatLayoutKinds.Sleeper, 2, 5, [2, 4], 17),
        new("Mẫu giường nằm 40 giường", SeatLayoutKinds.Sleeper, 2, 5, [2, 4], 20),
        new("Mẫu limousine 22 phòng", SeatLayoutKinds.Cabin, 2, 3, [2], 11),
        new("Mẫu limousine 24 phòng", SeatLayoutKinds.Cabin, 2, 3, [2], 12)
    ];

    public static async Task SeedAsync(TransportDbContext db, CancellationToken ct = default)
    {
        var existing = await db.SeatLayouts.Where(x => x.OrganizationId == null).Select(x => x.Name).ToListAsync(ct);
        foreach (var t in Templates.Where(t => !existing.Contains(t.Name)))
        {
            var layout = new SeatLayout { Name = t.Name, LayoutKind = t.Kind, DeckCount = t.Decks, Status = SeatLayoutStatuses.Locked };
            db.SeatLayouts.Add(layout);
            for (var deck = 1; deck <= t.Decks; deck++)
            {
                var cells = Build(t, deck);
                db.SeatDecks.Add(new SeatLayoutDeck { LayoutId = layout.Id, DeckNo = deck, Rows = cells.Max(x => x.Row), Columns = t.Columns });
                foreach (var cell in cells) { cell.LayoutId = layout.Id; db.SeatCells.Add(cell); }
            }
        }
        await db.SaveChangesAsync(ct);
    }

    // Hàng 1 tầng dưới: tài xế + cửa. Các hàng giữa chừa lối đi; hàng cuối dùng cả lối đi.
    private static List<SeatLayoutCell> Build(Template t, int deck)
    {
        var cells = new List<SeatLayoutCell>();
        var prefix = deck == 1 ? "A" : "B";
        var row = 1;
        if (deck == 1)
        {
            cells.Add(new SeatLayoutCell { DeckNo = deck, Row = 1, Col = 1, CellType = SeatCellTypes.Driver });
            cells.Add(new SeatLayoutCell { DeckNo = deck, Row = 1, Col = t.Columns, CellType = SeatCellTypes.Door });
            row = 2;
        }
        var seatColumns = Enumerable.Range(1, t.Columns).Where(c => !t.AisleColumns.Contains(c)).ToArray();
        var remaining = t.SeatsPerDeck;
        var number = 1;
        while (remaining > 0)
        {
            var lastRow = remaining <= t.Columns && remaining > seatColumns.Length;
            var columns = lastRow ? Enumerable.Range(1, t.Columns).ToArray() : seatColumns;
            foreach (var col in columns)
            {
                if (remaining == 0) break;
                cells.Add(new SeatLayoutCell
                {
                    DeckNo = deck, Row = row, Col = col, CellType = SeatCellTypes.Seat, Label = $"{prefix}{number++:00}",
                    SeatClass = SeatClasses.Standard, IsSellable = true
                });
                remaining--;
            }
            if (!lastRow)
                foreach (var aisle in t.AisleColumns)
                    cells.Add(new SeatLayoutCell { DeckNo = deck, Row = row, Col = aisle, CellType = SeatCellTypes.Aisle });
            row++;
        }
        return cells;
    }
}
