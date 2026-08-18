namespace Lazuar.Api.Middleware;

public sealed class SqlApiKeyCredentialLookup : IApiKeyCredentialLookup
{
    public Task<ApiKeyAuthenticationMiddleware.ApiKeyCacheEntry?> FindActiveAsync(
        IServiceProvider services,
        string keyHash)
        => ApiKeyAuthenticationMiddleware.LookupCredentialAsync(services, keyHash);
}
