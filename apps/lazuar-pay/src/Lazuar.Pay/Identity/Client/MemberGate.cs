using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;

namespace Lazuar.Pay.Identity.Client;

internal static class MemberGate
{
    public static async Task<IResult?> RequireMemberAsync(
        HttpRequest request,
        OneClient one,
        string orgId,
        CancellationToken cancellationToken)
    {
        if (!Bearer.TryGet(request, out var authorization))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }

        var wrongFamily = Bearer.RejectWrongFamily(authorization);
        if (wrongFamily is not null)
        {
            return wrongFamily;
        }

        if (string.IsNullOrWhiteSpace(orgId))
        {
            return PayErrors.Status(400, "Bad Request", "org_id is required");
        }

        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        if (Bearer.IsMachineKey(authorization))
        {
            return await RequireKeyBoundAsync(one, authorization, orgId, hint.ToString(), cancellationToken);
        }

        var result = await one.CheckMemberAsync(authorization, orgId, hint.ToString(), cancellationToken);
        if (result.StatusCode == 200 && result.Value)
        {
            return null;
        }

        if (result.TimedOut || result.TransportFailed)
        {
            return PayErrors.Status(503, "Service Unavailable", "Identity provider unreachable");
        }

        return result.StatusCode switch
        {
            401 => PayErrors.Status(401, "Unauthorized", "Identity provider rejected the token"),
            403 => PayErrors.Status(403, "Forbidden", ForbiddenDetail(result.Detail) ?? "Not a member of this org"),
            400 => PayErrors.Status(400, "Bad Request", string.IsNullOrWhiteSpace(result.Detail)
                ? "Identity provider rejected the request"
                : result.Detail),
            429 => PayErrors.Status(429, "Too Many Requests", "Identity provider rate limited"),
            200 => PayErrors.Status(403, "Forbidden", "Not a member of this org"),
            _ => PayErrors.Status(503, "Service Unavailable", "Identity provider failed")
        };
    }

    static string? ForbiddenDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        if (detail.IndexOf("suspend", StringComparison.OrdinalIgnoreCase) >= 0
            || detail.IndexOf("scope", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return detail.Trim();
        }

        return null;
    }

    static async Task<IResult?> RequireKeyBoundAsync(
        OneClient one,
        string authorization,
        string orgId,
        string? tenantHint,
        CancellationToken cancellationToken)
    {
        var who = await one.GetWhoamiAsync(authorization, tenantHint, cancellationToken);
        if (who.TimedOut || who.TransportFailed)
        {
            return PayErrors.Status(503, "Service Unavailable", "Identity provider unreachable");
        }

        if (who.StatusCode == 401)
        {
            return PayErrors.Status(401, "Unauthorized", "Identity provider rejected the token");
        }

        if (who.Value is null)
        {
            return who.StatusCode switch
            {
                403 => PayErrors.Status(403, "Forbidden", ForbiddenDetail(who.Detail) ?? "Not a member of this org"),
                400 => PayErrors.Status(400, "Bad Request", string.IsNullOrWhiteSpace(who.Detail)
                    ? "Identity provider rejected the request"
                    : who.Detail),
                429 => PayErrors.Status(429, "Too Many Requests", "Identity provider rate limited"),
                _ => PayErrors.Status(503, "Service Unavailable", "Identity provider failed")
            };
        }

        if (who.Value.Tenants.Count == 0)
        {
            return PayErrors.Status(403, "Forbidden", "Not a member of this org");
        }

        var tenant = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId);
        if (tenant is null)
        {
            return PayErrors.Status(403, "Forbidden", "Not a member of this org");
        }

        if (!string.Equals(tenant.Status, "active", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(tenant.Status))
        {
            return PayErrors.Status(403, "Forbidden", "Tenant is suspended.");
        }

        return null;
    }

    public static async Task<IResult?> RequireWriterAsync(
        HttpRequest request,
        OneClient one,
        string orgId,
        CancellationToken cancellationToken)
    {
        if (Bearer.TryGet(request, out var machine) && Bearer.IsMachineKey(machine))
        {
            return await RequireMemberAsync(request, one, orgId, cancellationToken);
        }

        var denied = await RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        Bearer.TryGet(request, out var authorization);
        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var who = await one.GetWhoamiAsync(authorization, hint.ToString(), cancellationToken);
        if (who.Value is null)
        {
            return PayErrors.Status(503, "Service Unavailable", "Identity provider failed");
        }

        var tenant = who.Value.Tenants.FirstOrDefault(t => t.Id == orgId);
        if (tenant is null)
        {
            return PayErrors.Status(403, "Forbidden", "Not a member of this org");
        }

        if (!string.Equals(tenant.Status, "active", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(tenant.Status))
        {
            return PayErrors.Status(403, "Forbidden", "Tenant is suspended.");
        }

        if (tenant.Role is not ("owner" or "admin"))
        {
            return PayErrors.Status(403, "Forbidden", "Writer role required");
        }

        return null;
    }
}
