// apps/lazuar-api/Modules/Community/Infrastructure/Endpoints.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.Community.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommunityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin/community").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/community");
        var authGroup = endpoints.MapGroup("/community").RequireAuthorization();

        adminGroup.MapPlanEndpoints();
        adminGroup.MapSubscriberEndpoints();
        adminGroup.MapReminderScheduleEndpoints();
        adminGroup.MapTemplateEndpoints();
        adminGroup.MapCouponEndpoints();
        adminGroup.MapBroadcastEndpoints();
        adminGroup.MapStatsEndpoints();
        adminGroup.MapPaymentConfigEndpoints();

        publicGroup.MapPublicEndpoints();
        
        authGroup.MapAuthenticatedCommunityEndpoints();

        return endpoints;
    }
}
