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

        if (string.IsNullOrWhiteSpace(orgId))
        {
            return PayErrors.Status(400, "Bad Request", "org_id is required");
        }

        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
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
            403 => PayErrors.Status(403, "Forbidden", "Not a member of this org"),
            200 => PayErrors.Status(403, "Forbidden", "Not a member of this org"),
            _ => PayErrors.Status(503, "Service Unavailable", "Identity provider failed")
        };
    }

    public static async Task<IResult?> RequireWriterAsync(
        HttpRequest request,
        OneClient one,
        string orgId,
        CancellationToken cancellationToken)
    {
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
