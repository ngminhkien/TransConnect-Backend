using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public class VerificationCodeService(
    IdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<VerificationOptions> verificationOptions) : IVerificationCodeService
{
    private readonly VerificationOptions _options = verificationOptions.Value;

    public int ExpiryMinutes => _options.ExpiryMinutes;

    public async Task<string> IssueAsync(
        Guid userId,
        VerificationPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);

        var recentCount = await dbContext.VerificationCodes
            .CountAsync(v => v.UserId == userId
                             && v.Purpose == purpose
                             && v.CreatedAt >= oneHourAgo, cancellationToken);

        if (recentCount >= _options.MaxRequestsPerHour)
        {
            throw new TooManyRequestsException(
                "Bạn đã yêu cầu mã quá nhiều lần. Vui lòng thử lại sau 1 giờ.");
        }

        // Vô hiệu hoá mã cũ: chỉ mã mới nhất được dùng, tránh trường hợp nhiều mã cùng sống
        // làm tăng số lần đoán trúng.
        var activeCodes = await dbContext.VerificationCodes
            .Where(v => v.UserId == userId && v.Purpose == purpose && !v.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var activeCode in activeCodes)
        {
            activeCode.IsUsed = true;
            activeCode.UsedAt = DateTime.UtcNow;
        }

        var code = GenerateCode(_options.CodeLength);

        dbContext.VerificationCodes.Add(new VerificationCode
        {
            UserId = userId,
            Purpose = purpose,
            CodeHash = passwordHasher.Hash(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return code;
    }

    public Task ConsumeAsync(
        Guid userId,
        VerificationPurpose purpose,
        string code,
        CancellationToken cancellationToken = default) =>
        ConsumeAndExecuteAsync(userId, purpose, code, _ => Task.CompletedTask, cancellationToken);

    public async Task ConsumeAndExecuteAsync(
        Guid userId,
        VerificationPurpose purpose,
        string code,
        Func<CancellationToken, Task> onVerified,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);
        var verificationCode = await dbContext.VerificationCodes.AsNoTracking()
            .Where(v => v.UserId == userId && v.Purpose == purpose && !v.IsUsed)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Dùng chung 1 thông báo cho mọi trường hợp hỏng (không có mã / sai / hết hạn):
        // nói rõ hơn chỉ giúp kẻ tấn công biết mình đang đi đúng hướng.
        if (verificationCode is null || verificationCode.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        if (verificationCode.AttemptCount >= _options.MaxAttempts)
        {
            await InvalidateAsync(verificationCode.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        if (!passwordHasher.Verify(code, verificationCode.CodeHash))
        {
            var now = DateTime.UtcNow;
            await dbContext.VerificationCodes.Where(v => v.Id == verificationCode.Id && !v.IsUsed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.AttemptCount, v => v.AttemptCount + 1)
                    .SetProperty(v => v.IsUsed, v => v.AttemptCount + 1 >= _options.MaxAttempts)
                    .SetProperty(v => v.UsedAt, v => v.AttemptCount + 1 >= _options.MaxAttempts ? now : (DateTime?)null)
                    .SetProperty(v => v.UpdatedAt, now), cancellationToken);
            // Không rollback lượt nhập sai khi trả 401 cho reset/verify-email.
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        var consumedAt = DateTime.UtcNow;
        var claimed = await dbContext.VerificationCodes
            .Where(v => v.Id == verificationCode.Id && !v.IsUsed
                && v.ExpiresAt > consumedAt && v.AttemptCount < _options.MaxAttempts)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsUsed, true)
                .SetProperty(v => v.UsedAt, consumedAt).SetProperty(v => v.UpdatedAt, consumedAt), cancellationToken);
        if (claimed != 1)
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");

        await onVerified(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task InvalidateAsync(Guid codeId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await dbContext.VerificationCodes.Where(v => v.Id == codeId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsUsed, true)
                .SetProperty(v => v.UsedAt, now).SetProperty(v => v.UpdatedAt, now), cancellationToken);
    }

    private async Task LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // No-op UPDATE lấy write lock đến khi transaction kết thúc, trước mọi truy vấn OTP.
        // Khóa trên User còn hoạt động tránh cả race cấp mã khi chưa có VerificationCode.
        // SQL Server khóa tại DB (nhiều instance); SQLite dùng write transaction trong test.
        var locked = await IdentityUserLock.AcquireAsync(dbContext, userId, cancellationToken);
        if (locked != 1)
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
    }

    // RandomNumberGenerator chứ không phải Random: Random đoán được nếu biết seed,
    // đủ để kẻ tấn công dựng lại mã của người khác.
    private static string GenerateCode(int length)
    {
        var upperBound = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, upperBound);

        // PadLeft để giữ số 0 ở đầu — "004512" phải là mã 6 ký tự, không phải "4512".
        return value.ToString().PadLeft(length, '0');
    }
}
