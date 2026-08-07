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
        SELECT "Id" AS "CredentialId", "OrganizationId", "Scopes"
        FROM one."ApiCredentials"
        WHERE "KeyHash" = @KeyHash AND "IsActive" = true
        LIMIT 1
        """;

    private const string LhdnLookupSql = """
        SELECT "Id" AS "CredentialId", "OrganizationId", "Scopes"
        FROM lhdn."DeveloperApiKeys"
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

            if (!_cache.TryGetValue(cacheKey, out ApiKeyCacheEntry? entry) || entry is null)
            {
                entry = await LookupCredentialAsync(context.RequestServices, keyHash);

                if (entry is null || entry.OrganizationId == Guid.Empty)
                {
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
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "api_client"),
                new("CredentialId", entry.CredentialId.ToString()),
                new("TenantId", entry.OrganizationId.ToString()),
                new("IsTestMode", isTestMode ? "true" : "false"),
                new(ClaimTypes.Role, "API_CLIENT")
            };

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
    /// Dual-read: platform keys in One first, then legacy Lhdn <c>DeveloperApiKeys</c>.
    /// </summary>
    internal static async Task<ApiKeyCacheEntry?> LookupCredentialAsync(IServiceProvider services, string keyHash)
    {
        // Prefer platform store (One)
        var oneFactory = services.GetKeyedService<ISqlConnectionFactory>("OneSqlConnectionFactory");
        if (oneFactory is not null)
        {
            using var oneConnection = oneFactory.CreateConnection();
            var oneResult = await oneConnection.QuerySingleOrDefaultAsync<ApiKeyCacheEntry>(
                OneLookupSql,
                new { KeyHash = keyHash });

            if (oneResult is not null && oneResult.OrganizationId != Guid.Empty)
            {
                return oneResult;
            }
        }

        // Fallback: legacy LHDN-local keys during migration window
        var lhdnFactory = services.GetKeyedService<ISqlConnectionFactory>("LhdnSqlConnectionFactory");
        if (lhdnFactory is not null)
        {
            using var lhdnConnection = lhdnFactory.CreateConnection();
            var lhdnResult = await lhdnConnection.QuerySingleOrDefaultAsync<ApiKeyCacheEntry>(
                LhdnLookupSql,
                new { KeyHash = keyHash });

            if (lhdnResult is not null && lhdnResult.OrganizationId != Guid.Empty)
            {
                return lhdnResult;
            }
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

    /// <summary>Cached principal material for an active developer API key.</summary>
    internal sealed class ApiKeyCacheEntry
    {
        public Guid CredentialId { get; init; }
        public Guid OrganizationId { get; init; }
        public string Scopes { get; init; } = string.Empty;
    }
}
