using TransConnect.Modules.Compliance.Dto.Requests;
using TransConnect.Modules.Compliance.Dto.Responses;

namespace TransConnect.Modules.Compliance.Services;

public interface IDemoDocumentService
{
    Task<DemoDocumentResponse> UploadAsync(UploadDemoDocumentRequest request, CancellationToken ct);
    Task<IReadOnlyList<DemoDocumentResponse>> ListAsync(CancellationToken ct);
    Task<DemoDocumentResponse> GetAsync(Guid id, CancellationToken ct);
    Task<DemoDocumentResponse> SimulateAsync(Guid id, SimulateDocumentRequest request, CancellationToken ct);
    Task<DemoDocumentResponse> ReviewAsync(Guid id, ReviewDemoDocumentRequest request, CancellationToken ct);
    Task<DemoDownloadResponse> DownloadAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
