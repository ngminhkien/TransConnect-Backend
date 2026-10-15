using Amazon.S3;
using Amazon.Runtime;
using Amazon.S3.Model;
using TransConnect.Base.Exceptions;

namespace TransConnect.Modules.Compliance.Storage;

public sealed class S3PrivateDocumentStorage(IAmazonS3 client) : IPrivateDocumentStorage
{
    public async Task PutAsync(string bucket, string key, byte[] content, string contentType, CancellationToken ct)
    {
        using var stream = new MemoryStream(content, writable: false);
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket, Key = key, InputStream = stream, ContentType = contentType,
                AutoCloseStream = false, UseChunkEncoding = false
            }, ct);
        }
        catch (AmazonS3Exception) { throw Unavailable(); }
        catch (AmazonClientException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (HttpRequestException) { throw Unavailable(); }
    }

    public async Task<byte[]> ReadAsync(string bucket, string key, int maxBytes, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetObjectAsync(bucket, key, ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await response.ResponseStream.ReadAsync(chunk, ct)) != 0)
            {
                if (buffer.Length + read > maxBytes) throw Unavailable();
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            return buffer.ToArray();
        }
        catch (AmazonS3Exception) { throw Unavailable(); }
        catch (AmazonClientException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (HttpRequestException) { throw Unavailable(); }
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken ct)
    {
        try { await client.DeleteObjectAsync(bucket, key, ct); }
        catch (AmazonS3Exception) { throw Unavailable(); }
        catch (AmazonClientException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (HttpRequestException) { throw Unavailable(); }
    }

    // Do not expose/log SDK response bodies, signed requests or endpoint credentials.
    private static ServiceUnavailableAppException Unavailable() => new("Kho file chưa sẵn sàng. Kiểm tra kết nối và quyền bucket.");
}
