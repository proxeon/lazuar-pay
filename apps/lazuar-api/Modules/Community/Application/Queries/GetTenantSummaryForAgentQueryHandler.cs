using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Contracts.Queries;

namespace Modules.Community.Application.Queries;

public class GetTenantSummaryForAgentQueryHandler : IQueryHandler<GetTenantSummaryForAgentQuery, string>
{
    private readonly ICommunityQueryService _queryService;

    public GetTenantSummaryForAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<string> Handle(GetTenantSummaryForAgentQuery request, CancellationToken ct)
    {
        var stats = await _queryService.GetSubscriberStatsAsync(request.OrganizationId);
        var plans = await _queryService.GetAdminPlansAsync(request.OrganizationId);

        var sb = new StringBuilder();
        sb.AppendLine("Community Ecosystem Summary:");
        sb.AppendLine($"- MRR: RM {stats.Mrr:F2}");
        sb.AppendLine($"- Active Subscribers: {stats.Active_subscribers}");
        sb.AppendLine($"- Past Due Subscribers: {stats.Past_due_subscribers}");
        sb.AppendLine($"- Churn Rate: {stats.Churn_rate_percentage}%");
        sb.AppendLine("- Active Plans:");

        foreach (var plan in plans)
        {
            sb.AppendLine($"  * '{plan.Name}' (ID: {plan.Id}) | RM {plan.Price:F2}/{plan.Interval} | Enrolled: {plan.Enrolled_count}");
        }

        return sb.ToString();
    }
}
