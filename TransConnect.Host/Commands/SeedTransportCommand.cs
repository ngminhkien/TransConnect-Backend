using Microsoft.EntityFrameworkCore;
using TransConnect.Modules.Transport.Persistence;

namespace TransConnect.Host.Commands;

/// <summary>
/// `dotnet run --project TransConnect.Host -- seed-transport`
/// Nạp mẫu sơ đồ ghế hệ thống (idempotent theo tên) sau khi đã apply migration Transport.
/// Không migrate, không bật `Transport:ApplyMigrationsOnStartup`, không đụng module khác.
/// </summary>
public static class SeedTransportCommand
{
    public const string Name = "seed-transport";

    public static bool TryParse(string[] args, out string[] hostArgs)
    {
        var index = Array.IndexOf(args, Name);
        hostArgs = index < 0 ? args : [.. args[..index], .. args[(index + 1)..]];
        return index >= 0;
    }

    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransportDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            Console.Error.WriteLine($"Transport còn {pending.Count} migration chưa apply; chạy migration trước rồi thử lại.");
            return 3;
        }

        var before = await db.SeatLayouts.CountAsync(x => x.OrganizationId == null, cancellationToken);
        await TransportSeeder.SeedAsync(db, cancellationToken);
        var after = await db.SeatLayouts.CountAsync(x => x.OrganizationId == null, cancellationToken);
        Console.WriteLine($"Mẫu sơ đồ ghế hệ thống: {after} (thêm mới {after - before}).");
        return 0;
    }
}
