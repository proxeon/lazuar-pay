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
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        return Results.Json(new OrgReadyResponse { OrgId = orgId, Ready = true }, OneClient.Json);
    }
}
