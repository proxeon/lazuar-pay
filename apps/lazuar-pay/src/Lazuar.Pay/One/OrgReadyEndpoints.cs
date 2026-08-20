namespace Lazuar.Pay.One;

internal static class OrgReadyEndpoints
{
    public static void MapOrgReady(this WebApplication app)
    {
        app.MapGet("/v1/orgs/{orgId}/ready", Handle);
    }

    static async Task<IResult> Handle(
        string orgId,
        HttpRequest request,
        OneClient one,
        CancellationToken cancellationToken)
    {
        if (!Bearer.TryGet(request, out var authorization))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }

        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var result = await one.CheckMemberAsync(authorization, orgId, hint.ToString(), cancellationToken);
        return Map(orgId, result);
    }

    internal static IResult Map(string orgId, OneCallResult<bool> result)
    {
        if (result.StatusCode == 200 && result.Value)
        {
            return Results.Json(new OrgReadyResponse { OrgId = orgId, Ready = true }, OneClient.Json);
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
