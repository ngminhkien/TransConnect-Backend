namespace TransConnect.Modules.Compliance.Storage;

public interface IPrivateDocumentStorage
{
    Task PutAsync(string bucket, string key, byte[] content, string contentType, CancellationToken ct);
    Task<byte[]> ReadAsync(string bucket, string key, int maxBytes, CancellationToken ct);
    Task DeleteAsync(string bucket, string key, CancellationToken ct);
}
