using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Lazuar.Pay.Identity.Client;

/// <summary>
/// Caches One <c>/me</c> by SHA-256 of the Authorization header.
/// Machine keys are also indexed by <c>key_id</c> (One <c>/me</c> <c>user_id</c>)
/// so Plane A <c>api_key.revoked</c> can drop them without waiting for TTL.
/// </summary>
public sealed class OneWhoamiCache(IMemoryCache memory)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _byKeyId = new(StringComparer.Ordinal);

    public static string TokenHash(string authorization)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(authorization));
        return Convert.ToHexString(bytes);
    }

    public bool TryGet(string authorization, out WhoamiResponse who)
    {
        who = null!;
        return memory.TryGetValue(CacheKey(TokenHash(authorization)), out who!) && who is not null;
    }

    public void Set(string authorization, WhoamiResponse who, bool machineKey)
    {
        var hash = TokenHash(authorization);
        memory.Set(CacheKey(hash), who, Ttl);
        if (machineKey && !string.IsNullOrWhiteSpace(who.UserId))
        {
            var set = _byKeyId.GetOrAdd(who.UserId, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
            set[hash] = 1;
        }
    }

    public void RemoveToken(string authorization)
    {
        memory.Remove(CacheKey(TokenHash(authorization)));
    }

    public void InvalidateKey(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return;
        }

        if (!_byKeyId.TryRemove(keyId, out var hashes))
        {
            return;
        }

        foreach (var hash in hashes.Keys)
        {
            memory.Remove(CacheKey(hash));
        }
    }

    static string CacheKey(string tokenHash) => "pay:whoami:" + tokenHash;
}
