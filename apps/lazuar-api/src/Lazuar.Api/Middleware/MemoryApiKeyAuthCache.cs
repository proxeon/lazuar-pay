using Microsoft.Extensions.Caching.Memory;
using Modules.One.Application;

namespace Lazuar.Api.Middleware;

public sealed class MemoryApiKeyAuthCache : IApiKeyAuthCache
{
    private readonly IMemoryCache _cache;

    public MemoryApiKeyAuthCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Evict(string keyHash)
    {
        if (string.IsNullOrWhiteSpace(keyHash))
        {
            return;
        }

        _cache.Remove(ApiKeyAuthenticationMiddleware.CacheKey(keyHash));
    }
}
