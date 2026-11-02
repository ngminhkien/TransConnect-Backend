using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Transport;
using TransConnect.Modules.Compliance.Dto.Requests;
using TransConnect.Modules.Compliance.Dto.Responses;
using TransConnect.Modules.Compliance.Entities;
using TransConnect.Modules.Compliance.Persistence;

namespace TransConnect.Modules.Compliance.Services;

public sealed class ProfileDocumentService(ComplianceDbContext db, DemoDocumentService files, IProfileDocumentAccess access) : IProfileDocumentService
{
    private static void ValidateType(string subjectType, string documentType)
    {
        if (!((subjectType == "Driver" && documentType == "DriverLicense")
            || (subjectType == "Vehicle" && documentType == "VehicleRegistration")))
            throw new ValidationAppException("Demo chỉ nhận Driver/DriverLicense và Vehicle/VehicleRegistration.");
    }

    public async Task<ProfileDocumentVersionResponse> UploadAsync(string subjectType, Guid subjectId, string documentType,
        UploadDemoDocumentRequest request, CancellationToken ct)
    {
        await files.AuthorizeAsync(ct);
        ValidateType(subjectType, documentType);
        var scope = await access.AuthorizeAsync(subjectType, subjectId, true, ct);
        var root = await db.DemoProfileDocuments.SingleOrDefaultAsync(x => x.SubjectType == subjectType && x.SubjectId == subjectId
            && x.DocumentType == documentType && x.ScopeOrganizationId == scope.OrganizationId, ct);
        if (root is null)
        {
            root = new DemoProfileDocument { SubjectType = subjectType, SubjectId = subjectId,
                DocumentType = documentType, ScopeOrganizationId = scope.OrganizationId };
            db.DemoProfileDocuments.Add(root);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(root).State = EntityState.Detached;
                var duplicate = await db.DemoProfileDocuments.AsNoTracking().AnyAsync(x => x.SubjectType == subjectType
                    && x.SubjectId == subjectId && x.DocumentType == documentType && x.ScopeOrganizationId == scope.OrganizationId, ct);
                if (duplicate) throw new ConflictException("Hồ sơ đang được tạo bởi yêu cầu khác. Vui lòng thử lại.");
                throw;
            }
        }
        var uploaded = await files.UploadProfileAsync(request, root.Id, ct);
        var version = await db.DemoDocuments.AsNoTracking().SingleAsync(x => x.Id == uploaded.Id, ct);
        return Response(root, version);
    }

    public async Task<IReadOnlyList<ProfileDocumentVersionResponse>> ListAsync(string subjectType, Guid subjectId, string documentType, CancellationToken ct)
    {
        await files.AuthorizeAsync(ct);
        ValidateType(subjectType, documentType);
        var scope = await access.AuthorizeAsync(subjectType, subjectId, false, ct);
        var root = await db.DemoProfileDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.SubjectType == subjectType && x.SubjectId == subjectId
            && x.DocumentType == documentType && x.ScopeOrganizationId == scope.OrganizationId, ct);
        if (root is null) return [];
        return (await db.DemoDocuments.AsNoTracking().Where(x => x.ProfileDocumentId == root.Id)
            .OrderByDescending(x => x.VersionNumber).Take(100).ToListAsync(ct)).Select(x => Response(root, x)).ToArray();
    }

    private async Task<(DemoProfileDocument Root, DemoDocument Version)> LoadAsync(Guid id, bool write, CancellationToken ct)
    {
        await files.AuthorizeAsync(ct);
        var version = await db.DemoDocuments.SingleOrDefaultAsync(x => x.Id == id && x.ProfileDocumentId != null, ct)
            ?? throw new NotFoundException("Không tìm thấy phiên bản giấy tờ demo.");
        var root = await db.DemoProfileDocuments.SingleAsync(x => x.Id == version.ProfileDocumentId, ct);
        var scope = await access.AuthorizeAsync(root.SubjectType, root.SubjectId, write, ct);
        if (scope.OrganizationId != root.ScopeOrganizationId) throw new ForbiddenAppException();
        return (root, version);
    }

    public async Task<ProfileDocumentVersionResponse> SimulateAsync(Guid versionId, SimulateDocumentRequest request, CancellationToken ct)
    {
        var (root, version) = await LoadAsync(versionId, true, ct);
        await files.SimulateCoreAsync(version, request, ct);
        return Response(root, version);
    }
    public async Task<ProfileDocumentVersionResponse> ReviewAsync(Guid versionId, ReviewDemoDocumentRequest request, CancellationToken ct)
    {
        var (root, version) = await LoadAsync(versionId, true, ct);
        await files.ReviewCoreAsync(version, request, ct);
        return Response(root, version);
    }
    public async Task<DemoDownloadResponse> DownloadAsync(Guid versionId, CancellationToken ct)
    {
        var (_, version) = await LoadAsync(versionId, false, ct);
        return await files.DownloadCoreAsync(version, ct);
    }
    public async Task DeleteAsync(Guid versionId, CancellationToken ct)
    {
        var (_, version) = await LoadAsync(versionId, true, ct);
        await files.DeleteCoreAsync(version, ct);
    }
    private static ProfileDocumentVersionResponse Response(DemoProfileDocument root, DemoDocument version) =>
        new(root.Id, root.SubjectType, root.SubjectId, root.DocumentType, version.VersionNumber!.Value, DemoDocumentService.ToResponse(version));
}
