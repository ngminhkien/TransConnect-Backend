using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Notifications;
using TransConnect.Common.Transport;
using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;
using TransConnect.Modules.Operator.Entities;
using TransConnect.Modules.Operator.Options;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class DriverService(OperatorDbContext db, OperatorAccess access, IAccountDirectory accounts,
    IEmailQueue emails, IOptions<OperatorOptions> options, ITransportObligations obligations) : IDriverService
{
    public async Task<DriverResponse> SubmitAsync(SubmitDriverRequest request, CancellationToken ct)
    {
        var account = await access.OperatorAsync(ct);
        if (await db.Drivers.IgnoreQueryFilters().AnyAsync(x => x.AccountId == account.AccountId, ct))
            throw new ConflictException("Tài khoản đã có hồ sơ tài xế.");
        var driver = new Driver { AccountId = account.AccountId, FullName = request.FullName.Trim() };
        db.Drivers.Add(driver);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            if (await db.Drivers.IgnoreQueryFilters().AnyAsync(x => x.AccountId == account.AccountId, ct))
                throw new ConflictException("Tài khoản đã có hồ sơ tài xế.");
            throw;
        }
        return Response(driver);
    }

    public async Task<DriverResponse> MeAsync(CancellationToken ct)
    {
        var account = await access.OperatorAsync(ct);
        return Response(await db.Drivers.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == account.AccountId, ct)
            ?? throw new NotFoundException("Chưa có hồ sơ tài xế."));
    }

    public async Task<IReadOnlyList<DriverResponse>> PendingAsync(CancellationToken ct)
    {
        await access.AdminAsync(ct);
        return (await db.Drivers.AsNoTracking().Where(x => x.ReviewStatus == "Pending").Take(200).ToListAsync(ct))
            .Select(Response).ToList();
    }

    public async Task<DriverResponse> ReviewAsync(Guid id, ReviewRequest request, CancellationToken ct)
    {
        var admin = await access.AdminAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Drivers.Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, x => x.UpdatedAt), ct);
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Tài xế", id);
        if (driver.ReviewStatus != "Pending") throw new ConflictException("Hồ sơ đã được xét duyệt.");
        driver.ReviewStatus = request.Approved ? "Approved" : "Rejected";
        driver.ReviewNote = request.Note;
        driver.ReviewedAt = DateTime.UtcNow;
        driver.ReviewedBy = admin.AccountId;
        access.Audit(Guid.Empty, id, "driver.review", driver.ReviewStatus);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(driver);
    }

    public async Task<InvitationResponse> InviteAsync(Guid organizationId, Guid driverId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(organizationId, ct);
        var org = await access.OrganizationAsync(organizationId, "manage", ct);
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == driverId, ct)
            ?? throw new NotFoundException("Tài xế", driverId);
        if (driver.ReviewStatus != "Approved") throw new ConflictException("Tài xế chưa được nền tảng xác minh.");
        var account = await accounts.FindActiveAsync(driver.AccountId, ct)
            ?? throw new ConflictException("Tài khoản tài xế không còn khả dụng.");
        var old = await db.Engagements.Where(x => x.OrganizationId == organizationId && x.DriverId == driverId
            && x.EndedAt == null && x.RevokedAt == null).ToListAsync(ct);
        if (old.Any(x => x.AcceptedAt != null)) throw new ConflictException("Tài xế đang cộng tác với tổ chức.");
        foreach (var item in old) item.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        if (org.DriverLimit is { } limit && await OccupancyAsync(organizationId, ct) >= limit)
            throw new ConflictException("Đã đạt hạn mức tài xế và lời mời tài xế còn hiệu lực.");
        var token = InvitationTokens.Create();
        var engagement = new DriverEngagement
        {
            OrganizationId = organizationId, DriverId = driverId, TokenHash = InvitationTokens.Hash(token),
            ExpiresAt = DateTime.UtcNow.AddHours(options.Value.InvitationHours)
        };
        db.Engagements.Add(engagement);
        access.Audit(organizationId, engagement.Id, "driver.invite");
        await db.SaveChangesAsync(ct);
        await emails.EnqueueAsync(new EmailRequest
        {
            ToEmail = account.Email, ToName = driver.FullName, TemplateCode = "organization-invitation",
            Placeholders = new Dictionary<string, string>
            {
                ["OrganizationName"] = org.Name, ["InvitationToken"] = token,
                ["ExpiresAt"] = engagement.ExpiresAt.ToString("O"), ["Role"] = "Driver"
            }
        }, ct);
        await tx.CommitAsync(ct);
        return new InvitationResponse(engagement.Id, engagement.ExpiresAt);
    }

    public async Task<InvitationResponse> InviteByEmailAsync(Guid organizationId, InviteDriverRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(organizationId, ct);
        var org = await access.OrganizationAsync(organizationId, "manage", ct);
        if (org.ReviewStatus != "Approved") throw new ConflictException("Tổ chức chưa được phê duyệt.");
        var email = request.Email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var old = await db.Invitations.Where(x => x.OrganizationId == organizationId && x.Role == "Driver"
            && x.Email == email && x.AcceptedAt == null && x.RevokedAt == null).ToListAsync(ct);
        foreach (var item in old) item.RevokedAt = now;
        await db.SaveChangesAsync(ct);
        if (org.DriverLimit is { } limit && await OccupancyAsync(organizationId, ct) >= limit)
            throw new ConflictException("Đã đạt hạn mức tài xế và lời mời tài xế còn hiệu lực.");
        var token = InvitationTokens.Create();
        var invitation = new OrganizationInvitation
        {
            OrganizationId = organizationId, Email = email, Role = "Driver",
            TokenHash = InvitationTokens.Hash(token), ExpiresAt = now.AddHours(options.Value.InvitationHours)
        };
        db.Invitations.Add(invitation);
        access.Audit(organizationId, invitation.Id, "driver.email-invite");
        await db.SaveChangesAsync(ct);
        await emails.EnqueueAsync(new EmailRequest
        {
            ToEmail = email, ToName = email, TemplateCode = "organization-invitation",
            Placeholders = new Dictionary<string, string>
            {
                ["OrganizationName"] = org.Name, ["InvitationToken"] = token,
                ["ExpiresAt"] = invitation.ExpiresAt.ToString("O"), ["Role"] = "Driver"
            }
        }, ct);
        await tx.CommitAsync(ct);
        return new(invitation.Id, invitation.ExpiresAt);
    }

    private async Task<EngagementResponse> AcceptEmailInvitationAsync(
        OrganizationInvitation candidate, AccountSummary account, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(candidate.OrganizationId, ct);
        var invitation = await db.Invitations.SingleAsync(x => x.Id == candidate.Id, ct);
        if (invitation.AcceptedAt != null || invitation.RevokedAt != null || invitation.ExpiresAt <= DateTime.UtcNow
            || invitation.Email != account.Email.ToLowerInvariant())
            throw new UnauthorizedAppException("Lời mời không hợp lệ hoặc không dành cho tài khoản này.");
        if (!await db.Organizations.AnyAsync(x => x.Id == invitation.OrganizationId && x.ReviewStatus == "Approved", ct))
            throw new ConflictException("Tổ chức chưa được phê duyệt.");
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.AccountId == account.AccountId && x.ReviewStatus == "Approved", ct)
            ?? throw new ConflictException("Vui lòng nộp hồ sơ tài xế và chờ nền tảng xác minh trước khi chấp nhận.");
        var old = await db.Engagements.Where(x => x.OrganizationId == invitation.OrganizationId
            && x.DriverId == driver.Id && x.EndedAt == null && x.RevokedAt == null).ToListAsync(ct);
        if (old.Any(x => x.AcceptedAt != null)) throw new ConflictException("Tài xế đã cộng tác với tổ chức.");
        foreach (var item in old) item.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedBy = account.AccountId;
        var engagement = new DriverEngagement
        {
            OrganizationId = invitation.OrganizationId, DriverId = driver.Id,
            TokenHash = invitation.TokenHash, ExpiresAt = invitation.ExpiresAt, AcceptedAt = invitation.AcceptedAt
        };
        db.Engagements.Add(engagement);
        access.Audit(invitation.OrganizationId, engagement.Id, "driver.accept");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(engagement);
    }

    public async Task<EngagementResponse> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        var account = await access.OperatorAsync(ct);
        var hash = InvitationTokens.Hash(request.Token);
        var emailInvitation = await db.Invitations.AsNoTracking().SingleOrDefaultAsync(
            x => x.TokenHash == hash && x.Role == "Driver", ct);
        if (emailInvitation is not null)
            return await AcceptEmailInvitationAsync(emailInvitation, account, ct);
        var candidate = await db.Engagements.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct)
            ?? throw new UnauthorizedAppException("Lời mời không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(candidate.OrganizationId, ct);
        var item = await db.Engagements.SingleAsync(x => x.Id == candidate.Id, ct);
        if (item.AcceptedAt != null || item.EndedAt != null || item.RevokedAt != null || item.ExpiresAt <= DateTime.UtcNow
            || !await db.Drivers.AnyAsync(x => x.Id == item.DriverId && x.AccountId == account.AccountId && x.ReviewStatus == "Approved", ct)
            || !await db.Organizations.AnyAsync(x => x.Id == item.OrganizationId && x.ReviewStatus == "Approved", ct))
            throw new UnauthorizedAppException("Lời mời không hợp lệ hoặc không dành cho tài khoản này.");
        item.AcceptedAt = DateTime.UtcNow;
        access.Audit(item.OrganizationId, item.Id, "driver.accept");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(item);
    }

    public async Task<IReadOnlyList<EngagementResponse>> ListAsync(Guid organizationId, CancellationToken ct)
    {
        await access.OrganizationAsync(organizationId, "read", ct);
        return (await db.Engagements.AsNoTracking().Where(x => x.OrganizationId == organizationId).ToListAsync(ct))
            .Select(Response).ToList();
    }

    public async Task EndAsync(Guid organizationId, Guid engagementId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(organizationId, ct);
        var account = await access.AccountAsync(ct);
        var item = await db.Engagements.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == engagementId, ct)
            ?? throw new NotFoundException("Quan hệ cộng tác", engagementId);
        if (!await db.Drivers.AnyAsync(x => x.Id == item.DriverId && x.AccountId == account.AccountId, ct))
            await access.OrganizationAsync(organizationId, "manage", ct);
        if (await obligations.HasDriverObligationsAsync(organizationId, item.DriverId, ct))
            throw new ConflictException("Tài xế còn nghĩa vụ vận chuyển chưa giải quyết.");
        if (item.AcceptedAt == null) item.RevokedAt ??= DateTime.UtcNow;
        else item.EndedAt ??= DateTime.UtcNow;
        access.Audit(organizationId, item.Id, "driver.end");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static DriverResponse Response(Driver x) => new(x.Id, x.AccountId, x.FullName, x.ReviewStatus, x.ReviewNote);
    private async Task<int> OccupancyAsync(Guid organizationId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await db.Engagements.CountAsync(x => x.OrganizationId == organizationId && x.EndedAt == null
            && x.RevokedAt == null && (x.AcceptedAt != null || x.ExpiresAt > now), ct)
            + await db.Invitations.CountAsync(x => x.OrganizationId == organizationId && x.Role == "Driver"
                && x.AcceptedAt == null && x.RevokedAt == null && x.ExpiresAt > now, ct);
    }
    private static EngagementResponse Response(DriverEngagement x) =>
        new(x.Id, x.OrganizationId, x.DriverId, x.ExpiresAt, x.AcceptedAt, x.EndedAt, x.RevokedAt);
}
