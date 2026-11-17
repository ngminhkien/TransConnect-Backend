using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity.Persistence;

public enum AdminBootstrapOutcome
{
    Created,
    RoleRestored,
    AlreadyAdmin
}

/// <summary>
/// Tạo hoặc khôi phục tài khoản InternalAdmin có role Admin. Dùng cho lệnh CLI `create-admin`,
/// thay cho việc seed tài khoản với mật khẩu cố định khi khởi động Host.
/// Không bao giờ đổi mật khẩu của tài khoản đã tồn tại.
/// </summary>
public static class AdminBootstrapper
{
    public const string InternalAdminGroup = "InternalAdmin";

    // Giai đoạn test: dùng chung quy tắc mật khẩu của hệ thống. Siết lại trước khi deploy.
    public const int MinimumAdminPasswordLength = PasswordRules.MinimumLength;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Trả về thông báo lỗi tiếng Việt, hoặc null nếu mật khẩu hợp lệ.</summary>
    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return "Cần mật khẩu để tạo Admin mới.";
        if (password.Length < MinimumAdminPasswordLength)
            return $"Mật khẩu Admin phải có ít nhất {MinimumAdminPasswordLength} ký tự.";
        if (password.Length > PasswordRules.MaximumLength)
            return $"Mật khẩu không được vượt quá {PasswordRules.MaximumLength} ký tự.";
        return null;
    }

    public static async Task<bool> AdminExistsAsync(
        IdentityDbContext context, string email, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        return await context.Users.AnyAsync(
            u => u.Email == normalized && u.AccountGroup == InternalAdminGroup, cancellationToken);
    }

    public static async Task<AdminBootstrapOutcome> CreateOrRestoreAsync(
        IdentityDbContext context,
        IPasswordHasher passwordHasher,
        string email,
        string fullName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!new EmailAddressAttribute().IsValid(normalizedEmail) || normalizedEmail.Length == 0)
            throw new ArgumentException("Email không hợp lệ.", nameof(email));

        // Role/permission phải có trước khi gán; seed idempotent nên gọi lại an toàn.
        await IdentitySeeder.SeedAsync(context, cancellationToken);
        var adminRole = await context.Roles.SingleAsync(r => r.Name == IdentitySeeder.AdminRoleName, cancellationToken);

        var user = await context.Users.FirstOrDefaultAsync(
            u => u.Email == normalizedEmail && u.AccountGroup == InternalAdminGroup, cancellationToken);

        if (user is not null)
        {
            if (!user.IsActive)
                throw new InvalidOperationException("Tài khoản Admin này đang bị khóa; mở khóa trước khi khôi phục quyền.");

            if (await context.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == adminRole.Id, cancellationToken))
                return AdminBootstrapOutcome.AlreadyAdmin;

            context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
            await context.SaveChangesAsync(cancellationToken);
            return AdminBootstrapOutcome.RoleRestored;
        }

        var passwordError = ValidatePassword(password);
        if (passwordError is not null)
            throw new ArgumentException(passwordError, nameof(password));

        var name = string.IsNullOrWhiteSpace(fullName) ? "System Admin" : fullName.Trim();
        user = new User
        {
            Email = normalizedEmail,
            AccountGroup = InternalAdminGroup,
            FullName = name,
            PasswordHash = passwordHasher.Hash(password!),
            IsActive = true,
            // Do người vận hành tạo trực tiếp, không đi qua luồng đăng ký/OTP.
            EmailVerifiedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        await context.SaveChangesAsync(cancellationToken);
        return AdminBootstrapOutcome.Created;
    }
}
