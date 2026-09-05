using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Lazuar.Pay.Identity.Client;

/// <summary>
/// Caches One <c>/me</c> by SHA-256 of the Authorization header, with per-key and per-org
/// reverse indexes so Plane A webhooks (<c>api_key.revoked</c>, <c>tenant.suspended</c>)
/// can drop affected entries without waiting for the TTL. Issue 010 (issues/003): each
/// token now records its own index memberships, so dropping one token touches only its
/// own entries — the previous design walked every key and org set on every eviction, O(N)
/// per expiring token and O(N²) per TTL window under load.
/// </summary>
public sealed class OneWhoamiCache(IMemoryCache memory)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    /// <summary>Immutable record of which index sets a hash belongs to.</summary>
    sealed class IndexEntry(string? keyId, string[] orgIds)
    {
        public readonly string? KeyId = keyId;
        public readonly string[] OrgIds = orgIds;
    }

    readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _byKeyId = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _byOrgId = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, IndexEntry> _index = new(StringComparer.Ordinal);

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
        var keyId = machineKey && !string.IsNullOrWhiteSpace(who.UserId) ? who.UserId : null;
        var orgIds = who.Tenants
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .Select(t => t.Id!)
            .ToArray();
        var entry = new IndexEntry(keyId, orgIds);

        // Index before caching: the eviction callback carries this exact entry instance, so
        // a stale callback from a previous generation of the same hash can never drop the
        // current index (ForgetFromIndexes compares instances before pruning).
        _index[hash] = entry;
        if (keyId is not null)
        {
            _byKeyId.GetOrAdd(keyId, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[hash] = 1;
        }

        foreach (var org in orgIds)
        {
            _byOrgId.GetOrAdd(org, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[hash] = 1;
        }

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl
        };
        options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = (_, _, _, _) => ForgetFromIndexes(hash, entry)
        });
        memory.Set(CacheKey(hash), who, options);
    }

    public void RemoveToken(string authorization)
    {
        Drop(TokenHash(authorization));
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
            ForgetFromIndexes(hash, _index.TryGetValue(hash, out var entry) ? entry : null);
            memory.Remove(CacheKey(hash));
        }
    }

    public void InvalidateOrg(string orgId)
    {
        if (string.IsNullOrWhiteSpace(orgId))
        {
            return;
        }

        if (!_byOrgId.TryRemove(orgId, out var hashes))
        {
            return;
        }

        foreach (var hash in hashes.Keys)
        {
            ForgetFromIndexes(hash, _index.TryGetValue(hash, out var entry) ? entry : null);
            memory.Remove(CacheKey(hash));
        }
    }

    /// <summary>
    /// Drop a hash from the cache and from the reverse indexes it registered under. The
    /// instance check is what makes stale eviction callbacks harmless: a newer Set for the
    /// same hash has already replaced the index, and the old callback must not prune it.
    /// The wholesale-removed set (from InvalidateKey/Org) simply reads as absent here.
    /// </summary>
    void ForgetFromIndexes(string hash, IndexEntry? entry)
    {
        if (entry is null || !_index.TryRemove(KeyValuePair.Create(hash, entry)))
        {
            return;
        }

        PruneSets(hash, entry);
    }

    void Drop(string hash)
    {
        var entry = _index.TryRemove(hash, out var idx) ? idx : null;
        memory.Remove(CacheKey(hash));
        if (entry is not null)
        {
            PruneSets(hash, entry);
        }
    }

    void PruneSets(string hash, IndexEntry entry)
    {
        if (entry.KeyId is not null && _byKeyId.TryGetValue(entry.KeyId, out var keySet))
        {
            keySet.TryRemove(hash, out _);
            if (keySet.IsEmpty)
            {
                _byKeyId.TryRemove(entry.KeyId, out _);
            }
        }

        foreach (var org in entry.OrgIds)
        {
            if (_byOrgId.TryGetValue(org, out var orgSet))
            {
                orgSet.TryRemove(hash, out _);
                if (orgSet.IsEmpty)
                {
                    _byOrgId.TryRemove(org, out _);
                }
            }
        }
    }

    /// <summary>Test observability: the direct index must retire entries with their tokens.</summary>
    internal int TrackedIndexEntries => _index.Count;

    static string CacheKey(string tokenHash) => "pay:whoami:" + tokenHash;
}
