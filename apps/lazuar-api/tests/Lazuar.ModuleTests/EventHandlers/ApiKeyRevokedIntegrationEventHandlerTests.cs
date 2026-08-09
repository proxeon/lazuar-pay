using System;
using System.Threading.Tasks;
using Lazuar.Api.EventHandlers;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using OneApiKeyRevoked = Modules.One.Contracts.Events.ApiKeyRevokedIntegrationEvent;

namespace Lazuar.ModuleTests.EventHandlers;

[TestFixture]
public class ApiKeyRevokedIntegrationEventHandlerTests
{
    [Test]
    public async Task HandleAsync_One_Event_Removes_ApiKey_Cache_Entry_By_Hash()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string keyHash = "abc123hash";
        var cacheKey = $"ApiKey_{keyHash}";

        cache.Set(cacheKey, new { TenantId = Guid.CreateVersion7() });
        Assert.That(cache.TryGetValue(cacheKey, out _), Is.True);

        var handler = new ApiKeyRevokedIntegrationEventHandler(cache);
        await handler.HandleAsync(new OneApiKeyRevoked(Guid.CreateVersion7(), keyHash));

        Assert.That(cache.TryGetValue(cacheKey, out _), Is.False);
    }

    [Test]
    public async Task HandleAsync_Does_Not_Remove_Unrelated_Cache_Keys()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("ApiKey_other", "keep-me");
        cache.Set("ApiKey_revoked", "drop-me");

        var handler = new ApiKeyRevokedIntegrationEventHandler(cache);
        await handler.HandleAsync(new OneApiKeyRevoked(Guid.CreateVersion7(), "revoked"));

        Assert.That(cache.TryGetValue("ApiKey_revoked", out _), Is.False);
        Assert.That(cache.TryGetValue("ApiKey_other", out var remaining), Is.True);
        Assert.That(remaining, Is.EqualTo("keep-me"));
    }
}
