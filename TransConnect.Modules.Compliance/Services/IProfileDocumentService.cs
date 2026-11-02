using TransConnect.Modules.Compliance.Dto.Requests;
using TransConnect.Modules.Compliance.Dto.Responses;

namespace TransConnect.Modules.Compliance.Services;

public interface IProfileDocumentService
{
    Task<ProfileDocumentVersionResponse> UploadAsync(string subjectType, Guid subjectId, string documentType, UploadDemoDocumentRequest request, CancellationToken ct);
    Task<IReadOnlyList<ProfileDocumentVersionResponse>> ListAsync(string subjectType, Guid subjectId, string documentType, CancellationToken ct);
    Task<ProfileDocumentVersionResponse> SimulateAsync(Guid versionId, SimulateDocumentRequest request, CancellationToken ct);
    Task<ProfileDocumentVersionResponse> ReviewAsync(Guid versionId, ReviewDemoDocumentRequest request, CancellationToken ct);
    Task<DemoDownloadResponse> DownloadAsync(Guid versionId, CancellationToken ct);
    Task DeleteAsync(Guid versionId, CancellationToken ct);
}
