// apps/lazuar-api/BuildingBlocks/Infrastructure/R2StorageService.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// No-op storage used when R2 is not configured so the API can still boot.
/// Calls fail with a clear error until R2_* env vars are set.
/// </summary>
public sealed class DisabledR2StorageService : IR2StorageService
{
    private static InvalidOperationException Disabled() =>
        new("Object storage is not configured. Set R2_ENDPOINT, R2_ACCESS_KEY, R2_SECRET_KEY, and R2_BUCKET_NAME.");

    public Task<string?> UploadAsync(Stream data, string bucket, string key, string contentType, CancellationToken ct = default)
        => throw Disabled();

    public string GetPresignedUploadUrl(string bucket, string key, string contentType, int expiryMinutes = 60)
        => throw Disabled();

    public string GetPresignedDownloadUrl(string bucket, string key, int expiryMinutes = 60)
        => throw Disabled();
}

public class R2StorageService : IR2StorageService
{
    private readonly IAmazonS3 _client;

    public R2StorageService(IAmazonS3 client)
    {
        _client = client;
    }

    public async Task<string?> UploadAsync(Stream data, string bucket, string key, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = data,
            ContentType = contentType,
            UseChunkEncoding = false
        };

        var response = await _client.PutObjectAsync(request, ct);
        return response.HttpStatusCode == System.Net.HttpStatusCode.OK ? key : null;
    }

    public string GetPresignedUploadUrl(string bucket, string key, string contentType, int expiryMinutes = 60)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            ContentType = contentType
        };

        return _client.GetPreSignedURL(request);
    }

    public string GetPresignedDownloadUrl(string bucket, string key, int expiryMinutes = 60)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };

        return _client.GetPreSignedURL(request);
    }
}
