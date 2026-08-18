namespace Lazuar.Api.Middleware;

/// <summary>
/// Authoritative active-key lookup. Middleware must not trust a warm cache without this.
/// </summary>
public interface IApiKeyCredentialLookup
{
    Task<ApiKeyAuthenticationMiddleware.ApiKeyCacheEntry?> FindActiveAsync(
        IServiceProvider services,
        string keyHash);
}
