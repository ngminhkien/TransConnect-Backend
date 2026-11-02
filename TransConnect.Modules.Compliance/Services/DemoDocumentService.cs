using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Modules.Compliance.Dto.Requests;
using TransConnect.Modules.Compliance.Dto.Responses;
using TransConnect.Modules.Compliance.Entities;
using TransConnect.Modules.Compliance.Options;
using TransConnect.Modules.Compliance.Persistence;
using TransConnect.Modules.Compliance.Storage;

namespace TransConnect.Modules.Compliance.Services;

public sealed class DemoDocumentService(ComplianceDbContext db, IPrivateDocumentStorage storage,
    ICurrentUser currentUser, IAccountDirectory accounts, IOptions<ComplianceDemoOptions> options,
    IHostEnvironment environment) : IDemoDocumentService
{
    public const int MaxBytes = 8 * 1024 * 1024;

    internal async Task<Guid> AuthorizeAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled || !environment.IsDevelopment())
            throw new NotFoundException("Chức năng demo chưa bật.");
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid accountId
            || await accounts.FindActiveAsync(accountId, ct) is null)
            throw new UnauthorizedAppException("Tài khoản không còn hoạt động. Vui lòng đăng nhập lại.");
        return accountId;
    }

    public Task<DemoDocumentResponse> UploadAsync(UploadDemoDocumentRequest request, CancellationToken ct) => UploadCoreAsync(request, null, ct);

    internal Task<DemoDocumentResponse> UploadProfileAsync(UploadDemoDocumentRequest request, Guid profileDocumentId, CancellationToken ct) =>
        UploadCoreAsync(request, profileDocumentId, ct);

    private async Task<DemoDocumentResponse> UploadCoreAsync(UploadDemoDocumentRequest request, Guid? profileDocumentId, CancellationToken ct)
    {
        var accountId = await AuthorizeAsync(ct);
        if (!request.SyntheticDataConfirmed)
            throw new ValidationAppException("Chỉ nhận dữ liệu giả lập. Vui lòng xác nhận trước khi upload.");
        if (request.File is null || request.File.Length is <= 0 or > MaxBytes)
            throw new ValidationAppException("File phải có dữ liệu và không vượt quá 8 MiB.");
        using var buffer = new MemoryStream();
        await using (var input = request.File.OpenReadStream())
        {
            var chunk = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(chunk, ct)) != 0)
            {
                if (buffer.Length + read > MaxBytes)
                    throw new ValidationAppException("File vượt quá 8 MiB.");
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
        }
        var content = buffer.ToArray();
        var mime = DetectType(content);
        var document = new DemoDocument
        {
            AccountId = accountId, ProfileDocumentId = profileDocumentId, Bucket = options.Value.Bucket, ContentType = mime,
            Size = content.Length, Sha256 = Convert.ToHexString(SHA256.HashData(content))
        };
        var folder = $"demo/accounts/{accountId:N}";
        if (profileDocumentId is { } profileId)
        {
            var profile = await db.DemoProfileDocuments.AsNoTracking().SingleAsync(x => x.Id == profileId, ct);
            folder = $"demo/profiles/{profile.SubjectType}/{profile.SubjectId:N}/{profile.DocumentType}/{profile.ScopeOrganizationId?.ToString("N") ?? "personal"}";
        }
        document.ObjectKey = $"{folder}/{document.Id:N}/original";
        await using (var reservation = profileDocumentId.HasValue ? await db.Database.BeginTransactionAsync(ct) : null)
        {
            if (profileDocumentId is { } rootId)
            {
                if (await db.DemoProfileDocuments.Where(x => x.Id == rootId).ExecuteUpdateAsync(
                    set => set.SetProperty(x => x.LastVersionNumber, x => x.LastVersionNumber + 1), ct) != 1)
                    throw new NotFoundException("Hồ sơ demo không còn khả dụng.");
                document.VersionNumber = await db.DemoProfileDocuments.AsNoTracking().Where(x => x.Id == rootId)
                    .Select(x => x.LastVersionNumber).SingleAsync(ct);
            }
            db.DemoDocuments.Add(document);
            await SaveAsync(ct);
            if (reservation is not null) await reservation.CommitAsync(ct);
        }
        try
        {
            await storage.PutAsync(document.Bucket, document.ObjectKey, content, mime, ct);
        }
        catch (ServiceUnavailableAppException)
        {
            document.Status = "UploadFailed";
            await SaveAsync(ct);
            throw;
        }
        document.Status = "PendingSimulation";
        await SaveAsync(ct);
        return ToResponse(document);
    }

    public async Task<IReadOnlyList<DemoDocumentResponse>> ListAsync(CancellationToken ct)
    {
        var accountId = await AuthorizeAsync(ct);
        var documents = await db.DemoDocuments.AsNoTracking().Where(x => x.AccountId == accountId && x.ProfileDocumentId == null)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        return documents.Select(ToResponse).ToArray();
    }

    public async Task<DemoDocumentResponse> GetAsync(Guid id, CancellationToken ct) => ToResponse(await OwnedAsync(id, ct));

    public async Task<DemoDocumentResponse> SimulateAsync(Guid id, SimulateDocumentRequest request, CancellationToken ct) =>
        await SimulateCoreAsync(await OwnedAsync(id, ct), request, ct);

    internal async Task<DemoDocumentResponse> SimulateCoreAsync(DemoDocument document, SimulateDocumentRequest request, CancellationToken ct)
    {
        if (request.Scenario is not ("Success" or "Infected" or "ScannerUnavailable" or "OcrFailed"))
            throw new ValidationAppException("Kịch bản mô phỏng không hợp lệ.");
        if (document.Status != "PendingSimulation")
        {
            if (document.Scenario == request.Scenario && document.Status != "Deleting") return ToResponse(document);
            throw new ConflictException("File không ở trạng thái chờ mô phỏng. Tải file mới để thử kịch bản khác.");
        }
        // No file parsing, ClamAV, OCR, external AI, or promotion to a clean bucket.
        document.Scenario = request.Scenario;
        document.SimulatedAt = DateTime.UtcNow;
        document.ScanResult = request.Scenario switch
        {
            "Infected" => "SIMULATED_INFECTED",
            "ScannerUnavailable" => "SIMULATED_UNAVAILABLE",
            _ => "SIMULATED_CLEAN"
        };
        document.Status = request.Scenario switch
        {
            "Success" => "DemoNeedsReview",
            "Infected" => "DemoBlocked",
            _ => "DemoFailed"
        };
        document.OcrText = request.Scenario == "Success"
            ? "[OCR MÔ PHỎNG] Họ tên: NGUYỄN VĂN DEMO. Mã hồ sơ: DEMO-0001. Nội dung cố định, không trích xuất từ file."
            : null;
        await SaveAsync(ct);
        return ToResponse(document);
    }

    public async Task<DemoDocumentResponse> ReviewAsync(Guid id, ReviewDemoDocumentRequest request, CancellationToken ct) =>
        await ReviewCoreAsync(await OwnedAsync(id, ct), request, ct);

    internal async Task<DemoDocumentResponse> ReviewCoreAsync(DemoDocument document, ReviewDemoDocumentRequest request, CancellationToken ct)
    {
        if (request.Approved is null) throw new ValidationAppException("Vui lòng chọn kết quả duyệt demo.");
        var status = request.Approved.Value ? "DemoApproved" : "DemoRejected";
        if (document.Status == status) return ToResponse(document);
        if (document.Status != "DemoNeedsReview")
            throw new ConflictException("Chỉ duyệt demo sau kịch bản mô phỏng thành công.");
        document.Status = status;
        document.ReviewedAt = DateTime.UtcNow;
        await SaveAsync(ct);
        return ToResponse(document);
    }

    public async Task<DemoDownloadResponse> DownloadAsync(Guid id, CancellationToken ct) =>
        await DownloadCoreAsync(await OwnedAsync(id, ct), ct);

    internal async Task<DemoDownloadResponse> DownloadCoreAsync(DemoDocument document, CancellationToken ct)
    {
        if (document.Status is not ("DemoNeedsReview" or "DemoApproved"))
            throw new ForbiddenAppException("Trạng thái demo này không cho tải file.");
        var content = await storage.ReadAsync(document.Bucket, document.ObjectKey, MaxBytes, ct);
        if (content.LongLength != document.Size || Convert.ToHexString(SHA256.HashData(content)) != document.Sha256)
            throw new ConflictException("File trong kho đã thay đổi. Không thể tải bản demo này.");
        // Attachment only, no inline rendering of unscanned files.
        return new DemoDownloadResponse(content, $"demo-unscanned-{document.Id:N}.bin");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct) =>
        await DeleteCoreAsync(await OwnedAsync(id, ct), ct);

    internal async Task DeleteCoreAsync(DemoDocument document, CancellationToken ct)
    {
        document.Status = "Deleting";
        await SaveAsync(ct);
        // Retry DELETE after storage/DB failure is safe; S3 delete is idempotent.
        await storage.DeleteAsync(document.Bucket, document.ObjectKey, ct);
        document.DeletedAt = DateTime.UtcNow;
        document.OcrText = null;
        await SaveAsync(ct);
    }

    private async Task<DemoDocument> OwnedAsync(Guid id, CancellationToken ct)
    {
        var accountId = await AuthorizeAsync(ct);
        return await db.DemoDocuments.SingleOrDefaultAsync(x => x.Id == id && x.AccountId == accountId && x.ProfileDocumentId == null, ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu demo.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        foreach (var entry in db.ChangeTracker.Entries<DemoDocument>().Where(x => x.State == EntityState.Modified))
            entry.Entity.Revision = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new ConflictException("Tài liệu vừa thay đổi. Vui lòng tải lại trạng thái."); }
    }

    private static string DetectType(byte[] data)
    {
        if (data.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (data.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (data.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        throw new ValidationAppException("Chỉ nhận file mẫu PNG, JPEG hoặc PDF. Kiểm tra đầu file không thay thế quét mã độc.");
    }

    internal static DemoDocumentResponse ToResponse(DemoDocument d) => new(d.Id, d.Status, d.ContentType,
        d.Size, d.Sha256, d.Scenario, d.ScanResult, d.OcrText, d.CreatedAt, d.SimulatedAt, d.ReviewedAt);
}
