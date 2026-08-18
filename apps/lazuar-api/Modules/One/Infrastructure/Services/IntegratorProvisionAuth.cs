using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Modules.One.Infrastructure.Configuration;

namespace Modules.One.Infrastructure.Services;

/// <summary>
/// Auth for <c>POST /one/integrations/workspaces/provision</c>:
/// bootstrap secret (header/bearer) <b>or</b> platform admin JWT (`is_system_admin=true`).
/// </summary>
public static class IntegratorProvisionAuth
{
    public const string ProvisionKeyHeader = "X-Lazuar-Provision-Key";
    public const string ProductAura = "aura";
    public const string DefaultBootstrapKeyName = "Aura bootstrap";

    public sealed record AuthResult(bool IsAuthorized, bool IsSuperAdmin, Guid? ActorUserId, string? FailureReason, int StatusCode);

    public static AuthResult Evaluate(HttpContext http, IntegratorProvisionSettings settings)
    {
        var configured = settings.Secret?.Trim() ?? string.Empty;
        var hasConfiguredSecret = !string.IsNullOrEmpty(configured);

        // 1) Explicit provision header (preferred for Aura).
        if (http.Request.Headers.TryGetValue(ProvisionKeyHeader, out var headerValues))
        {
            var presented = headerValues.ToString().Trim();
            if (!string.IsNullOrEmpty(presented))
            {
                if (!hasConfiguredSecret)
                {
                    return new AuthResult(false, false, null, "Provision secret is not configured on this server.", StatusCodes.Status401Unauthorized);
                }

                if (!FixedTimeEqualsUtf8(configured, presented))
                {
                    return new AuthResult(false, false, null, "Invalid provision credentials.", StatusCodes.Status401Unauthorized);
                }

                return new AuthResult(true, false, null, null, StatusCodes.Status200OK);
            }
        }

        // 2) Authorization: Bearer <provision-secret> (non-JWT secrets; JWTs fall through to SUPER_ADMIN).
        var auth = http.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth["Bearer ".Length..].Trim();
            if (!string.IsNullOrEmpty(token) && token.Split('.').Length != 3)
            {
                if (!hasConfiguredSecret)
                {
                    return new AuthResult(false, false, null, "Provision secret is not configured on this server.", StatusCodes.Status401Unauthorized);
                }

                if (!FixedTimeEqualsUtf8(configured, token))
                {
                    return new AuthResult(false, false, null, "Invalid provision credentials.", StatusCodes.Status401Unauthorized);
                }

                return new AuthResult(true, false, null, null, StatusCodes.Status200OK);
            }
        }

        // 3) Support / manual: platform admin JWT (cookie or Bearer). Membership
        // SUPER_ADMIN is tenant-scoped and is injected by TenantSecurityMiddleware
        // when X-Tenant-Id is present — that must not mint a provision grant.
        var principal = http.User;
        if (principal?.Identity?.IsAuthenticated == true)
        {
            var isSystemAdmin =
                string.Equals(principal.FindFirst("is_system_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);

            if (isSystemAdmin)
            {
                Guid? actor = null;
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    actor = userId;
                }

                return new AuthResult(true, true, actor, null, StatusCodes.Status200OK);
            }

            // Authenticated but not a platform admin (CLIENT, workspace SUPER_ADMIN, API key, etc.).
            return new AuthResult(false, false, null, "Provision requires a platform admin or a valid integrator provision secret.", StatusCodes.Status403Forbidden);
        }

        return new AuthResult(false, false, null, "Missing provision credentials.", StatusCodes.Status401Unauthorized);
    }

    private static bool FixedTimeEqualsUtf8(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        if (left.Length != right.Length)
        {
            // Compare against self-length to reduce timing leak of length.
            return CryptographicOperations.FixedTimeEquals(left, left) && false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
