using System.Security.Claims;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Domain;

namespace Lazuar.Api.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ITokenGeneratorService _tokenGenerator;

    private const string OneLookupSql = """
        SELECT "Id" AS "CredentialId", "OrganizationId", "Scopes", "Name"
        FROM one."ApiCredentials"
        WHERE "KeyHash" = @KeyHash AND "IsActive" = true
        LIMIT 1
        """;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, IMemoryCache cache, ITokenGeneratorService tokenGenerator)
    {
        _next = next;
        _cache = cache;
        _tokenGenerator = tokenGenerator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (TryGetApiKey(context.Request, out var token))
        {
            var isTestMode = token.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase);
            var keyHash = _tokenGenerator.HashToken(token);
            var cacheKey = $"ApiKey_{keyHash}";

            // Cache is a hint only. Revoke must win even if the outbox never evicts
            // this replica — re-read IsActive=true on every request.
            var lookup = context.RequestServices.GetService<IApiKeyCredentialLookup>();
            var entry = lookup is not null
                ? await lookup.FindActiveAsync(context.RequestServices, keyHash)
                : await LookupCredentialAsync(context.RequestServices, keyHash);

            if (entry is null || entry.OrganizationId == Guid.Empty)
            {
                _cache.Remove(cacheKey);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "Invalid or revoked API Key." });
                return;
            }

            _cache.Set(cacheKey, entry, TimeSpan.FromMinutes(5));

            var tenantKeysKey = $"TenantKeys_{entry.OrganizationId}";
            if (!_cache.TryGetValue(tenantKeysKey, out List<string>? keyHashes) || keyHashes is null)
            {
                keyHashes = new List<string>();
            }

            lock (keyHashes)
            {
                if (!keyHashes.Contains(keyHash))
                {
                    keyHashes.Add(keyHash);
                }
            }

            _cache.Set(tenantKeysKey, keyHashes, TimeSpan.FromMinutes(10));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "api_client"),
                new("CredentialId", entry.CredentialId.ToString()),
                new("TenantId", entry.OrganizationId.ToString()),
                new("IsTestMode", isTestMode ? "true" : "false"),
                new(ClaimTypes.Role, "API_CLIENT")
            };

            if (!string.IsNullOrEmpty(entry.Name))
            {
                claims.Add(new Claim("CredentialName", entry.Name));
            }

            foreach (var scope in PlatformApiScopes.Split(entry.Scopes))
            {
                claims.Add(new Claim("scope", scope));
            }

            var identity = new ClaimsIdentity(claims, "ApiKey");
            context.User = new ClaimsPrincipal(identity);

            context.Items["TenantId"] = entry.OrganizationId;
            context.Items["CredentialId"] = entry.CredentialId;
        }

        await _next(context);
    }

    /// <summary>
    /// One-only credential lookup (R05 / remaining-005 F03).
    /// <list type="bullet">
    /// <item><description>Reads <b>only</b> <c>one.ApiCredentials</c> via keyed <c>OneSqlConnectionFactory</c>.</description></item>
    /// <item><description>Legacy Lhdn dual-read is <b>removed</b> — Lhdn-only keys get 401.</description></item>
    /// <item>
    /// <description>
    /// <b>DEPLOY ONLY</b> after target env inventory Q8 <c>active_legacy_only = 0</c>
    /// (or signed residual quarantine). Shipping this code before that gate 401s residual Lhdn-only integrators.
    /// </description>
    /// </item>
    /// <item><description>Legacy Lhdn key table drop / archive is <b>R06</b> (≥ 30 days after One-only in prod) — not this change.</description></item>
    /// </list>
    /// See <c>plans/005-remaining/r05-notes.md</c>, <c>plans/004-maintenance/api-key-cutover-design.md</c>.
    /// </summary>
    internal static async Task<ApiKeyCacheEntry?> LookupCredentialAsync(IServiceProvider services, string keyHash)
    {
        var oneFactory = services.GetKeyedService<ISqlConnectionFactory>("OneSqlConnectionFactory");
        if (oneFactory is null)
        {
            return null;
        }

        using var oneConnection = oneFactory.CreateConnection();
        var oneResult = await oneConnection.QuerySingleOrDefaultAsync<ApiKeyCacheEntry>(
            OneLookupSql,
            new { KeyHash = keyHash });

        if (oneResult is not null && oneResult.OrganizationId != Guid.Empty)
        {
            return oneResult;
        }

        return null;
    }

    /// <summary>
    /// Accepts <c>Authorization: Bearer sk_live_|sk_test_...</c> or raw <c>Authorization: sk_...</c>.
    /// </summary>
    public static bool TryGetApiKey(HttpRequest request, out string apiKey)
    {
        apiKey = string.Empty;

        if (!request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return false;
        }

        var value = authHeader.ToString().Trim();
        if (value.Length == 0)
        {
            return false;
        }

        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            value = value["Bearer ".Length..].Trim();
        }

        if (value.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = value;
            return true;
        }

        return false;
    }

    public static string CacheKey(string keyHash) => $"ApiKey_{keyHash}";

    /// <summary>Cached principal material for an active developer API key.</summary>
    public sealed class ApiKeyCacheEntry
    {
        public Guid CredentialId { get; init; }
        public Guid OrganizationId { get; init; }
        public string Scopes { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }
}
