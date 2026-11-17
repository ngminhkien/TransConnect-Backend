using System.Text;
using Microsoft.EntityFrameworkCore;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Host.Commands;

/// <summary>
/// `dotnet run --project TransConnect.Host -- create-admin --email a@b.c [--name "Tên"]`
/// Tạo Admin đầu tiên hoặc khôi phục role Admin cho tài khoản InternalAdmin có sẵn.
/// Mật khẩu lấy từ biến môi trường TRANSCONNECT_ADMIN_PASSWORD hoặc nhập ẩn trên console;
/// không bao giờ nhận qua tham số dòng lệnh (lộ trong lịch sử shell/danh sách tiến trình).
/// </summary>
public sealed class CreateAdminCommand
{
    public const string Name = "create-admin";
    public const string PasswordEnvironmentVariable = "TRANSCONNECT_ADMIN_PASSWORD";

    public string? Email { get; private init; }
    public string FullName { get; private init; } = "System Admin";
    public string? Error { get; private init; }

    /// <summary>
    /// Tách lệnh khỏi args. Trả về null nếu args không chứa lệnh; hostArgs là phần còn lại
    /// (ví dụ --environment) để truyền cho WebApplication.CreateBuilder.
    /// </summary>
    public static CreateAdminCommand? TryParse(string[] args, out string[] hostArgs)
    {
        var index = Array.IndexOf(args, Name);
        if (index < 0)
        {
            hostArgs = args;
            return null;
        }

        hostArgs = args[..index];
        string? email = null;
        string? name = null;
        var rest = args[(index + 1)..];
        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--email" when i + 1 < rest.Length:
                    email = rest[++i];
                    break;
                case "--name" when i + 1 < rest.Length:
                    name = rest[++i];
                    break;
                case "--password":
                    return new CreateAdminCommand
                    {
                        Error = $"Không nhận mật khẩu qua tham số. Đặt biến môi trường {PasswordEnvironmentVariable} hoặc để lệnh hỏi mật khẩu."
                    };
                default:
                    return new CreateAdminCommand { Error = $"Tham số không hợp lệ: {rest[i]}" };
            }
        }

        if (string.IsNullOrWhiteSpace(email))
            return new CreateAdminCommand { Error = "Thiếu --email." };

        return new CreateAdminCommand { Email = email, FullName = name ?? "System Admin" };
    }

    public async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            Console.Error.WriteLine(Error);
            PrintUsage();
            return 2;
        }

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Lệnh này không tự đổi schema; DB phải được migrate trước bằng quy trình hiện hành.
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            Console.Error.WriteLine($"Identity còn {pending.Count} migration chưa apply; chạy migration trước rồi thử lại.");
            return 3;
        }

        string? password = null;
        if (!await AdminBootstrapper.AdminExistsAsync(context, Email!, cancellationToken))
        {
            password = ReadPassword();
            var passwordError = AdminBootstrapper.ValidatePassword(password);
            if (passwordError is not null)
            {
                Console.Error.WriteLine(passwordError);
                return 2;
            }
        }

        try
        {
            var outcome = await AdminBootstrapper.CreateOrRestoreAsync(
                context, hasher, Email!, FullName, password, cancellationToken);
            var email = AdminBootstrapper.NormalizeEmail(Email!);
            Console.WriteLine(outcome switch
            {
                AdminBootstrapOutcome.Created => $"Đã tạo Admin {email}.",
                AdminBootstrapOutcome.RoleRestored => $"Đã khôi phục role Admin cho {email} (mật khẩu giữ nguyên).",
                _ => $"{email} đã là Admin; không thay đổi gì (mật khẩu giữ nguyên)."
            });
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static string? ReadPassword()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
        if (!string.IsNullOrEmpty(fromEnvironment))
            return fromEnvironment;

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine($"Không có console để nhập mật khẩu; đặt biến môi trường {PasswordEnvironmentVariable}.");
            return null;
        }

        var first = ReadHidden($"Mật khẩu Admin (ít nhất {AdminBootstrapper.MinimumAdminPasswordLength} ký tự): ");
        var second = ReadHidden("Nhập lại mật khẩu: ");
        if (first != second)
        {
            Console.Error.WriteLine("Hai lần nhập không khớp.");
            return null;
        }

        return first;
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var buffer = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0) buffer.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar))
                buffer.Append(key.KeyChar);
        }

        Console.WriteLine();
        return buffer.ToString();
    }

    private static void PrintUsage() => Console.Error.WriteLine(
        $"Cách dùng: dotnet run --project TransConnect.Host -- {Name} --email <email> [--name \"Tên hiển thị\"]");
}
