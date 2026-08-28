using Lazuar.Pay.Data;
using Lazuar.Pay.Hosting;
using Lazuar.Pay.Identity.Client;
using Lazuar.Pay.Rails;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Identity;

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
        PayDbContext db,
        IHostEnvironment env,
        CancellationToken cancellationToken)
    {
        var denied = await MemberGate.RequireMemberAsync(request, one, orgId, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var settings = await db.OrgSettings.FindAsync([orgId], cancellationToken);
        var hasVault = await db.GatewayCredentials.AnyAsync(x => x.OrgId == orgId, cancellationToken);
        var ready = IsReady(settings?.ChargesPaused == true, hasVault, PayProviders.AllowsTest(env));
        return Results.Json(new OrgReadyResponse { OrgId = orgId, Ready = ready }, OneClient.Json);
    }

    internal static bool IsReady(bool chargesPaused, bool hasVault, bool allowsTest) =>
        !chargesPaused && (hasVault || allowsTest);
}
