using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.Billing.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/billing").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/billing");

        admin.MapAdminLedgerEndpoints();
        admin.MapAdminCreditsEndpoints();
        admin.MapAdminSaasEndpoints();
        admin.MapAdminProfileEndpoints();
        publicGroup.MapPublicBillingEndpoints();

        return endpoints;
    }
}
