namespace Lazuar.Pay.One;

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
}
