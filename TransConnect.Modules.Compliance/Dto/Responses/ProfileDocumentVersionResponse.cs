namespace TransConnect.Modules.Compliance.Dto.Responses;

public sealed record ProfileDocumentVersionResponse(Guid ProfileDocumentId, string SubjectType, Guid SubjectId,
    string DocumentType, int VersionNumber, DemoDocumentResponse File);
