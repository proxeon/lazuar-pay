using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Contracts;

namespace Modules.Community.Application.Queries;

public class GetTenantSummaryForAgentQueryHandler : IQueryHandler<GetTenantSummaryForAgentQuery, string>
{
    private readonly ICommunityQueryService _queryService;
    private readonly IBillingQueryService _billingQueryService;

    public GetTenantSummaryForAgentQueryHandler(
        ICommunityQueryService queryService,
        IBillingQueryService billingQueryService)
    {
        _queryService = queryService;
        _billingQueryService = billingQueryService;
    }

    public async Task<string> Handle(GetTenantSummaryForAgentQuery request, CancellationToken ct)
    {
        var stats = await _queryService.GetSubscriberStatsAsync(request.OrganizationId);
        var plans = await _queryService.GetAdminPlansAsync(request.OrganizationId);
        var financials = await _billingQueryService.GetFinancialSummaryAsync(request.OrganizationId);

        var sb = new StringBuilder();
        sb.AppendLine("Community Ecosystem Summary:");
        sb.AppendLine($"- Active Subscribers: {stats.Active_subscribers}");
        sb.AppendLine($"- Past Due Subscribers: {stats.Past_due_subscribers}");
        sb.AppendLine($"- Cancellation Rate: {stats.Churn_rate_percentage}%");
        
        sb.AppendLine("\nFinancial Health (Ledger):");
        sb.AppendLine($"- Gross Revenue: RM {financials.Gross_revenue:F2}");
        sb.AppendLine($"- Net Cash in Bank: RM {financials.Net_revenue:F2}");
        sb.AppendLine($"- Total Gateway Fees Paid: RM {financials.Total_gateway_fees:F2}");
        sb.AppendLine($"- Tax Liabilities Owed: RM {financials.Total_tax_liabilities:F2}");
        sb.AppendLine($"- Deferred Revenue: RM {financials.Deferred_revenue:F2}");
        
        sb.AppendLine("\nActive Plans:");
        foreach (var plan in plans)
        {
            sb.AppendLine($"  * Name: '{plan.Name}' (ID: {plan.Id}) | Price: RM {plan.Price:F2}/{plan.Interval} | Enrolled: {plan.Enrolled_count}");
        }
        return sb.ToString();
    }
}
