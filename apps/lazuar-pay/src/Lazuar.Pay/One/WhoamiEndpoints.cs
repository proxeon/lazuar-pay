namespace Lazuar.Pay.One;

internal static class WhoamiEndpoints
{
    public static void MapWhoami(this WebApplication app)
    {
        app.MapGet("/v1/whoami", Handle);
    }

    static async Task<IResult> Handle(HttpRequest request, OneClient one, CancellationToken cancellationToken)
    {
        if (!Bearer.TryGet(request, out var authorization))
        {
            return PayErrors.Status(401, "Unauthorized", "Missing bearer token");
        }

        request.Headers.TryGetValue("X-Lazuar-Tenant-Id", out var hint);
        var result = await one.GetWhoamiAsync(authorization, hint.ToString(), cancellationToken);
        return Map(result);
    }

    internal static IResult Map(OneCallResult<WhoamiResponse> result)
    {
        if (result.Value is not null && result.StatusCode == 200)
        {
            return Results.Json(result.Value, OneClient.Json);
        }

        if (result.TimedOut || result.TransportFailed)
        {
            return PayErrors.Status(503, "Service Unavailable", "Identity provider unreachable");
        }

        return result.StatusCode switch
        {
            401 => PayErrors.Status(401, "Unauthorized", "Identity provider rejected the token"),
            403 => PayErrors.Status(403, "Forbidden", "Identity provider forbade this caller"),
            _ => PayErrors.Status(503, "Service Unavailable", "Identity provider failed")
        };
    }
}
