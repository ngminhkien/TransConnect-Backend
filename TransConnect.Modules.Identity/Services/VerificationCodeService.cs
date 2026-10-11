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

        return code;
    }

    public async Task ConsumeAsync(
        Guid userId,
        VerificationPurpose purpose,
        string code,
        CancellationToken cancellationToken = default)
    {
        var verificationCode = await dbContext.VerificationCodes
            .Where(v => v.UserId == userId && v.Purpose == purpose && !v.IsUsed)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Dùng chung 1 thông báo cho mọi trường hợp hỏng (không có mã / sai / hết hạn):
        // nói rõ hơn chỉ giúp kẻ tấn công biết mình đang đi đúng hướng.
        if (verificationCode is null || verificationCode.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        if (verificationCode.AttemptCount >= _options.MaxAttempts)
        {
            await InvalidateAsync(verificationCode, cancellationToken);
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        if (!passwordHasher.Verify(code, verificationCode.CodeHash))
        {
            verificationCode.AttemptCount++;

            // Vừa chạm ngưỡng thì huỷ luôn, không để kẻ tấn công thử tiếp bằng mã này.
            if (verificationCode.AttemptCount >= _options.MaxAttempts)
            {
                verificationCode.IsUsed = true;
                verificationCode.UsedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        verificationCode.IsUsed = true;
        verificationCode.UsedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task InvalidateAsync(VerificationCode code, CancellationToken cancellationToken)
    {
        code.IsUsed = true;
        code.UsedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
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
