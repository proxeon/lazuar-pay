namespace BuildingBlocks.Application;

/// <summary>
/// Thin object-storage port (R2/S3-compatible). Shared by Billing + One.
/// Implementation lives in BuildingBlocks.Infrastructure (real client or disabled no-op).
/// </summary>
public interface IR2StorageService
{
    Task<string?> UploadAsync(Stream data, string bucket, string key, string contentType, CancellationToken ct = default);
    string GetPresignedUploadUrl(string bucket, string key, string contentType, int expiryMinutes = 60);
    string GetPresignedDownloadUrl(string bucket, string key, int expiryMinutes = 60);
}
