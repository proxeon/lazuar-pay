using System.Security.Claims;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Api.Middleware;

/// <summary>
/// Intercepts requests bearing LHDN API Keys, resolves them securely via in-memory caching 
/// to protect database throughput, and constructs the tenant execution context.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ITokenGeneratorService _tokenGenerator;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, IMemoryCache cache, ITokenGeneratorService tokenGenerator)
    {
        _next = next;
        _cache = cache;
        _tokenGenerator = tokenGenerator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authHeaderString = authHeader.ToString();
            
            if (authHeaderString.StartsWith("Bearer sk_live_", StringComparison.OrdinalIgnoreCase) ||
                authHeaderString.StartsWith("Bearer sk_test_", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeaderString.Substring("Bearer ".Length).Trim();
                var isTestMode = token.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase);
                
                var keyHash = _tokenGenerator.HashToken(token);
                var cacheKey = $"ApiKey_{keyHash}";

                if (!_cache.TryGetValue(cacheKey, out Guid tenantId))
                {
                    var connectionFactory = context.RequestServices.GetRequiredKeyedService<ISqlConnectionFactory>("LhdnSqlConnectionFactory");
                    using var connection = connectionFactory.CreateConnection();
                    
                    var query = @"SELECT ""OrganizationId"" FROM lhdn.""DeveloperApiKeys"" WHERE ""KeyHash"" = @KeyHash AND ""IsActive"" = true LIMIT 1";
                    var result = await connection.QuerySingleOrDefaultAsync<Guid?>(query, new { KeyHash = keyHash });

                    if (result == null || result == Guid.Empty)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new { error = "Invalid or revoked API Key." });
                        return; 
                    }

                    tenantId = result.Value;
                    _cache.Set(cacheKey, tenantId, TimeSpan.FromMinutes(5));
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, "api_client"),
                    new Claim("TenantId", tenantId.ToString()),
                    new Claim("IsTestMode", isTestMode ? "true" : "false"),
                    new Claim(ClaimTypes.Role, "API_CLIENT")
                };

                var identity = new ClaimsIdentity(claims, "ApiKey");
                context.User = new ClaimsPrincipal(identity);
                
                context.Items["TenantId"] = tenantId;
            }
        }

        await _next(context);
    }
}
