using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Billing.Contracts;

namespace Modules.Billing.Application.Queries.Agent;

[AgentTool("Retrieve the exact financial health of the workspace, including Gross Revenue, Net Cash in Bank, Total Gateway Fees paid, and Tax Liabilities owed to the government.", "low", "SUPER_ADMIN", "ADMIN")]
public record GetFinancialHealthAgentQuery(Guid OrganizationId) : IQuery<AgentFinancialHealthResult>;

public record AgentFinancialHealthResult(
    double GrossRevenue,
    double NetCashInBank,
    double TotalGatewayFees,
    double TaxLiabilities,
    double DeferredRevenue,
    string Currency
);

public class GetFinancialHealthAgentQueryHandler : IQueryHandler<GetFinancialHealthAgentQuery, AgentFinancialHealthResult>
{
    private readonly IBillingQueryService _billingQueryService;

    public GetFinancialHealthAgentQueryHandler(IBillingQueryService billingQueryService)
    {
        _billingQueryService = billingQueryService;
    }

    public async Task<AgentFinancialHealthResult> Handle(GetFinancialHealthAgentQuery request, CancellationToken cancellationToken)
    {
        var summary = await _billingQueryService.GetFinancialSummaryAsync(request.OrganizationId);
        return new AgentFinancialHealthResult(
            (double)summary.Gross_revenue,
            (double)summary.Net_revenue,
            (double)summary.Total_gateway_fees,
            (double)summary.Total_tax_liabilities,
            (double)summary.Deferred_revenue,
            summary.Currency
        );
    }
}
