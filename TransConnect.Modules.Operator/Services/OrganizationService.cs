using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;
using TransConnect.Modules.Operator.Entities;
using TransConnect.Modules.Operator.Options;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class OrganizationService(OperatorDbContext db, OperatorAccess access,
    ICurrentUser current, IEmailQueue emails, IOptions<OperatorOptions> options, IAccountDirectory accounts) : IOrganizationService
{
    public async Task<OrganizationResponse> SubmitAsync(SubmitOrganizationRequest request, CancellationToken ct)
    {
        ValidateSubmission(request);
        var account = await access.AccountAsync(ct);
        if (account.Group == AccountGroups.InternalAdmin) await access.AdminAsync(ct);
        else account = await access.OperatorAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAccountAsync(account.AccountId, ct);
        if (account.Group == AccountGroups.Operator)
            await CheckApplicantAsync(account.AccountId, ct);
        if (account.Group == AccountGroups.InternalAdmin && (string.IsNullOrWhiteSpace(request.OwnerEmail)
            || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.OwnerEmail)))
            throw new ValidationAppException("Nộp thay cần email người nhận Owner.");
        var org = new Organization
        {
            CreatedByUserId = account.Group == AccountGroups.Operator ? account.AccountId : null,
            Name = request.Name.Trim(), RegistrationNumber = request.RegistrationNumber.Trim().ToUpperInvariant(),
            ProposedOwnerEmail = account.Group == AccountGroups.Operator ? account.Email : request.OwnerEmail.Trim().ToLowerInvariant(),
            EmployeeLimit = options.Value.DefaultEmployeeLimit,
            DriverLimit = options.Value.DefaultDriverLimit
        };
        if (await db.Organizations.IgnoreQueryFilters().AnyAsync(x => x.RegistrationNumber == org.RegistrationNumber, ct))
            throw new ConflictException("Mã đăng ký tổ chức đã tồn tại.");
        db.Organizations.Add(org);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            if (await db.Organizations.IgnoreQueryFilters().AnyAsync(x => x.RegistrationNumber == org.RegistrationNumber, ct))
                throw new ConflictException("Mã đăng ký tổ chức đã tồn tại.");
            throw;
        }
        await tx.CommitAsync(ct);
        return Response(org);
    }

    private static void ValidateSubmission(SubmitOrganizationRequest request)
    {
        ProfileValidation.Check(request);
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.RegistrationNumber, @"\A(?:[0-9]{10}|[0-9]{13})\z"))
            throw new ValidationAppException(new Dictionary<string, string[]> { ["registrationNumber"] = ["Mã số thuế phải gồm 10 hoặc 13 chữ số."] });
    }

    private async Task CheckApplicantAsync(Guid accountId, CancellationToken ct)
    {
        if (await db.Drivers.IgnoreQueryFilters().AnyAsync(x => x.AccountId == accountId, ct)
            || await db.Members.IgnoreQueryFilters().AnyAsync(x => x.AccountId == accountId, ct))
            throw new ConflictException("Người nhận Owner cần tài khoản Operator riêng, chưa thuộc tổ chức và chưa có hồ sơ tài xế.");
    }

    public async Task<OrganizationResponse> ResubmitAsync(Guid id, SubmitOrganizationRequest request, CancellationToken ct)
    {
        ValidateSubmission(request);
        var account = await access.AccountAsync(ct);
        var legacyAdmin = account.Group == AccountGroups.InternalAdmin;
        if (legacyAdmin) await access.AdminAsync(ct);
        else account = await access.OperatorAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAccountAsync(account.AccountId, ct);
        await access.LockAsync(id, ct);
        var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id
            && (x.CreatedByUserId == account.AccountId || legacyAdmin && x.CreatedByUserId == null), ct)
            ?? throw new NotFoundException("Hồ sơ tổ chức", id);
        if (org.ReviewStatus != "Rejected") throw new ConflictException("Chỉ nộp lại hồ sơ bị từ chối.");
        if (!legacyAdmin) await CheckApplicantAsync(account.AccountId, ct);
        else
        {
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.OwnerEmail)
                || string.IsNullOrWhiteSpace(request.OwnerEmail))
                throw new ValidationAppException("Nộp thay cần email người nhận Owner.");
            org.ProposedOwnerEmail = request.OwnerEmail.Trim().ToLowerInvariant();
        }
        if (await db.Organizations.IgnoreQueryFilters().AnyAsync(x => x.Id != id && x.RegistrationNumber == request.RegistrationNumber, ct))
            throw new ConflictException("Mã số thuế đã tồn tại.");
        org.Name = request.Name.Trim();
        org.RegistrationNumber = request.RegistrationNumber;
        org.ReviewStatus = "Pending";
        db.ProfileReviews.Add(new() { SubjectType = "Organization", SubjectId = id, Status = "Pending", ActorAccountId = account.AccountId });
        access.Audit(id, id, "organization.resubmit");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(org);
    }

    public async Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken ct)
    {
        var account = await access.AccountAsync(ct);
        var query = db.Organizations.AsNoTracking();
        if (account.Group != AccountGroups.InternalAdmin || !current.HasPermission("organizations.manage"))
            query = query.Where(x => x.CreatedByUserId == account.AccountId || db.Members.Any(m => m.OrganizationId == x.Id
                && m.AccountId == account.AccountId && m.IsActive));
        return (await query.OrderBy(x => x.Name).Take(200).ToListAsync(ct)).Select(Response).ToList();
    }

    public async Task<OrganizationResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var account = await access.AccountAsync(ct);
        var application = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CreatedByUserId == account.AccountId, ct);
        return Response(application ?? await access.OrganizationAsync(id, "read", ct));
    }

    public async Task<OrganizationResponse> UpdateProfileAsync(Guid id, UpdateOrganizationProfileRequest request, CancellationToken ct)
    {
        ProfileValidation.Check(request);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(id, ct);
        var org = await access.OrganizationAsync(id, "owner", ct);
        org.LegalEntityType = request.LegalEntityType;
        org.RegisteredAddress = request.RegisteredAddress.Trim();
        org.ContactPhone = request.ContactPhone.Trim();
        org.ContactEmail = request.ContactEmail.Trim().ToLowerInvariant();
        access.Audit(id, id, "organization.profile-update");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(org);
    }

    public async Task<OrganizationResponse> ReviewAsync(Guid id, ReviewRequest request, CancellationToken ct)
    {
        ProfileValidation.Check(request);
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Note))
            throw new ValidationAppException("Vui lòng nhập lý do từ chối.");
        var admin = await access.AdminAsync(ct);
        var candidate = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Tổ chức", id);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (candidate.CreatedByUserId is { } applicant) await access.LockAccountAsync(applicant, ct);
        await access.LockAsync(id, ct);
        var org = await access.OrganizationAsync(id, "owner", ct);
        if (org.ReviewStatus != "Pending") throw new ConflictException("Hồ sơ đã được xét duyệt.");
        org.ReviewStatus = request.Approved ? "Approved" : "Rejected";
        org.ReviewNote = request.Note;
        org.ReviewedBy = admin.AccountId;
        org.ReviewedAt = DateTime.UtcNow;
        access.Audit(id, id, "organization.review", org.ReviewStatus);
        db.ProfileReviews.Add(new() { SubjectType = "Organization", SubjectId = id,
            Status = org.ReviewStatus, Note = request.Note, ActorAccountId = admin.AccountId });
        if (request.Approved)
        {
            if (org.CreatedByUserId is { } applicantId)
            {
                var applicantAccount = await accounts.FindActiveAsync(applicantId, ct);
                if (applicantAccount is null || applicantAccount.Group != AccountGroups.Operator || !applicantAccount.EmailVerified)
                    throw new ConflictException("Tài khoản người nộp chưa đủ điều kiện nhận Owner.");
                await CheckApplicantAsync(applicantId, ct);
                if (org.EmployeeLimit < 1) throw new ConflictException("Hạn mức cần đủ cho Owner.");
                db.Members.Add(new() { OrganizationId = id, AccountId = applicantId, Role = "OrganizationOwner" });
                access.Audit(id, applicantId, "member.accept", "OrganizationOwner");
            }
            else await CreateInvitationAsync(org, org.ProposedOwnerEmail, "OrganizationOwner", ct);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(org);
    }

    public async Task<InvitationResponse> InviteOwnerAsync(Guid id, CancellationToken ct)
    {
        await access.AdminAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(id, ct);
        var org = await access.OrganizationAsync(id, "owner", ct);
        if (await db.Members.AnyAsync(x => x.OrganizationId == id && x.Role == "OrganizationOwner", ct))
            throw new ConflictException("Tổ chức đã có Owner.");
        var result = await CreateInvitationAsync(org, org.ProposedOwnerEmail, "OrganizationOwner", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<InvitationResponse> InviteAsync(Guid id, InviteMemberRequest request, CancellationToken ct)
    {
        if (request.Role is not ("Manager" or "Dispatcher")) throw new ValidationAppException("Vai trò không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(id, ct);
        var org = await access.OrganizationAsync(id, "owner", ct);
        var result = await CreateInvitationAsync(org, request.Email.Trim().ToLowerInvariant(), request.Role, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<InvitationResponse> CreateInvitationAsync(Organization org, string email, string role, CancellationToken ct)
    {
        if (org.ReviewStatus != "Approved") throw new ConflictException("Tổ chức chưa được phê duyệt.");
        var now = DateTime.UtcNow;
        // Reissue invalidates the old token and keeps a single quota reservation.
        var old = await db.Invitations.Where(x => x.OrganizationId == org.Id && x.Email == email && x.Role != "Driver"
            && x.AcceptedAt == null && x.RevokedAt == null).ToListAsync(ct);
        foreach (var invitation in old) invitation.RevokedAt = now;
        await db.SaveChangesAsync(ct);
        if (await OccupancyAsync(org.Id, now, ct) >= org.EmployeeLimit)
            throw new ConflictException("Đã đạt hạn mức nhân viên và lời mời còn hiệu lực.");
        var token = InvitationTokens.Create();
        var item = new OrganizationInvitation
        {
            OrganizationId = org.Id, Email = email, Role = role, TokenHash = InvitationTokens.Hash(token),
            ExpiresAt = now.AddHours(options.Value.InvitationHours)
        };
        db.Invitations.Add(item);
        access.Audit(org.Id, item.Id, "member.invite", role);
        await db.SaveChangesAsync(ct);
        // If enqueue fails the organization transaction rolls back. An email queued before
        // a later rollback is harmless: accepting it still requires a committed invitation.
        await emails.EnqueueAsync(new EmailRequest
        {
            ToEmail = email, ToName = email, TemplateCode = "organization-invitation",
            SourceType = EmailSourceTypes.Invitation, SourceId = item.Id, DeliverBefore = item.ExpiresAt,
            Placeholders = new Dictionary<string, string>
            {
                ["OrganizationName"] = org.Name, ["InvitationToken"] = token,
                ["ExpiresAt"] = item.ExpiresAt.ToString("O"), ["Role"] = role
            }
        }, ct);
        return new InvitationResponse(item.Id, item.ExpiresAt);
    }

    public async Task<MemberResponse> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        var account = await access.OperatorAsync(ct);
        var hash = InvitationTokens.Hash(request.Token);
        var candidate = await db.Invitations.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash && x.Role != "Driver", ct)
            ?? throw new UnauthorizedAppException("Lời mời không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAccountAsync(account.AccountId, ct);
        await access.LockAsync(candidate.OrganizationId, ct);
        var invitation = await db.Invitations.SingleAsync(x => x.Id == candidate.Id, ct);
        var now = DateTime.UtcNow;
        if (invitation.AcceptedAt != null || invitation.RevokedAt != null || invitation.ExpiresAt <= now
            || invitation.Email != account.Email.ToLowerInvariant())
            throw new UnauthorizedAppException("Lời mời không hợp lệ hoặc không dành cho tài khoản này.");
        var org = await db.Organizations.SingleAsync(x => x.Id == invitation.OrganizationId, ct);
        if (org.ReviewStatus != "Approved") throw new ConflictException("Tổ chức chưa hoạt động.");
        if (invitation.Role == "OrganizationOwner")
        {
            if (invitation.Email != org.ProposedOwnerEmail
                || await db.Drivers.IgnoreQueryFilters().AnyAsync(x => x.AccountId == account.AccountId, ct))
                throw new ConflictException("Tài khoản nhận Owner phải đúng email đăng ký và không có hồ sơ tài xế.");
            if (await db.Members.AnyAsync(x => x.OrganizationId == org.Id && x.IsActive
                && x.Role == "OrganizationOwner", ct))
                throw new ConflictException("Tổ chức đã có Owner hoạt động.");
        }
        // Fixed organization membership; driver engagements are a separate many-to-many relation.
        if (await db.Members.IgnoreQueryFilters().AnyAsync(x => x.AccountId == account.AccountId, ct))
            throw new ConflictException("Tài khoản đã có tư cách thành viên; Owner cần quản lý tư cách hiện có.");
        var member = new OrganizationMember
        {
            OrganizationId = org.Id, AccountId = account.AccountId, Role = invitation.Role
        };
        invitation.AcceptedAt = now;
        invitation.AcceptedBy = account.AccountId;
        db.Members.Add(member);
        access.Audit(org.Id, member.Id, "member.accept", member.Role);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            if (await db.Members.IgnoreQueryFilters().AnyAsync(x => x.AccountId == account.AccountId, ct))
                throw new ConflictException("Tài khoản đã thuộc một tổ chức.");
            throw;
        }
        await tx.CommitAsync(ct);
        return Response(member);
    }

    public async Task RevokeAsync(Guid id, Guid invitationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(id, ct);
        await access.OrganizationAsync(id, "owner", ct);
        var item = await db.Invitations.SingleOrDefaultAsync(x => x.OrganizationId == id && x.Id == invitationId, ct)
            ?? throw new NotFoundException("Lời mời", invitationId);
        if (item.AcceptedAt != null) throw new ConflictException("Lời mời đã được chấp nhận.");
        item.RevokedAt ??= DateTime.UtcNow;
        access.Audit(id, invitationId, "member.invitation.revoke");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<MemberResponse>> MembersAsync(Guid id, CancellationToken ct)
    {
        await access.OrganizationAsync(id, "owner", ct);
        return await db.Members.AsNoTracking().Where(x => x.OrganizationId == id)
            .Select(x => new MemberResponse(x.Id, x.AccountId, x.Role, x.IsActive)).ToListAsync(ct);
    }

    public async Task<MemberResponse> UpdateMemberAsync(Guid id, Guid memberId, UpdateMemberRequest request, CancellationToken ct)
    {
        if (request.Role is not ("Manager" or "Dispatcher")) throw new ValidationAppException("Vai trò không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(id, ct);
        var org = await access.OrganizationAsync(id, "owner", ct);
        var member = await db.Members.SingleOrDefaultAsync(x => x.OrganizationId == id && x.Id == memberId, ct)
            ?? throw new NotFoundException("Thành viên", memberId);
        if (member.Role == "OrganizationOwner") throw new ConflictException("Không thay đổi Owner qua API nhân viên.");
        if (!member.IsActive && request.IsActive && await OccupancyAsync(id, DateTime.UtcNow, ct) >= org.EmployeeLimit)
            throw new ConflictException("Đã đạt hạn mức nhân viên.");
        var before = member.Role + "/" + member.IsActive;
        member.Role = request.Role;
        member.IsActive = request.IsActive;
        access.Audit(id, memberId, "member.update", before + " -> " + member.Role + "/" + member.IsActive);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(member);
    }

    public async Task SetLimitAsync(Guid id, EmployeeLimitRequest request, CancellationToken ct)
    {
        await access.AdminAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(id, ct);
        var org = await access.OrganizationAsync(id, "owner", ct);
        if (request.Limit < 1 || request.Limit < await OccupancyAsync(id, DateTime.UtcNow, ct))
            throw new ConflictException("Hạn mức thấp hơn số nhân viên và lời mời đang có.");
        access.Audit(id, id, "organization.quota", org.EmployeeLimit + " -> " + request.Limit);
        org.EmployeeLimit = request.Limit;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AuditResponse>> AuditAsync(Guid id, CancellationToken ct)
    {
        await access.OrganizationAsync(id, "owner", ct);
        return await db.AuditLogs.AsNoTracking().Where(x => x.OrganizationId == id)
            .OrderByDescending(x => x.CreatedAt).Take(200)
            .Select(x => new AuditResponse(x.Id, x.ActorAccountId, x.SubjectId, x.Action, x.Details, x.CreatedAt,
                x.SubjectType, x.RequestId)).ToListAsync(ct);
    }

    public async Task SetDriverLimitAsync(Guid id, DriverLimitRequest request, CancellationToken ct)
    {
        await access.AdminAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(id, ct);
        var org = await access.OrganizationAsync(id, "owner", ct);
        var now = DateTime.UtcNow;
        var count = await db.Engagements.CountAsync(x => x.OrganizationId == id && x.EndedAt == null
            && x.RevokedAt == null && (x.AcceptedAt != null || x.ExpiresAt > now), ct)
            + await db.Invitations.CountAsync(x => x.OrganizationId == id && x.Role == "Driver"
                && x.AcceptedAt == null && x.RevokedAt == null && x.ExpiresAt > now, ct);
        if (request.Limit is { } limit && (limit < 1 || limit < count))
            throw new ConflictException("Hạn mức thấp hơn số tài xế và lời mời đang có.");
        access.Audit(id, id, "organization.driver-quota", org.DriverLimit + " -> " + request.Limit);
        org.DriverLimit = request.Limit;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<int> OccupancyAsync(Guid id, DateTime now, CancellationToken ct) =>
        await db.Members.CountAsync(x => x.OrganizationId == id && x.IsActive, ct)
        + await db.Invitations.CountAsync(x => x.OrganizationId == id && x.Role != "Driver" && x.AcceptedAt == null
            && x.RevokedAt == null && x.ExpiresAt > now, ct);

    private static OrganizationResponse Response(Organization x) =>
        new(x.Id, x.Name, x.RegistrationNumber, x.ProposedOwnerEmail, x.ReviewStatus, x.ReviewNote, x.EmployeeLimit, x.DriverLimit,
            x.LegalEntityType, x.RegisteredAddress, x.ContactPhone, x.ContactEmail,
            x.LegalEntityType != null && x.RegisteredAddress != null && x.ContactPhone != null && x.ContactEmail != null, x.CreatedByUserId);
    private static MemberResponse Response(OrganizationMember x) => new(x.Id, x.AccountId, x.Role, x.IsActive);
}
