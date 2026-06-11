using Amazon.S3;
using Amazon.S3.Model;

namespace BuildingBlocks.Infrastructure;

public interface IR2StorageService
{
    Task<string?> UploadAsync(Stream data, string bucket, string key, string contentType, CancellationToken ct = default);
}

public class R2StorageService : IR2StorageService
{
    private readonly IAmazonS3 _client;

    public R2StorageService(IAmazonS3 client) => _client = client;

    public async Task<string?> UploadAsync(Stream data, string bucket, string key, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = data,
            ContentType = contentType
        };

        var response = await _client.PutObjectAsync(request, ct);
        return response.HttpStatusCode == System.Net.HttpStatusCode.OK ? key : null;
    }
}
