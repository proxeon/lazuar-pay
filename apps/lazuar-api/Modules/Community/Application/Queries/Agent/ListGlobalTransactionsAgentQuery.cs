using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Application.Queries;
using Modules.CRM.Contracts;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("Query the global financial ledger. Filter by date range, status (e.g., CONFIRMED, REFUNDED), or payment method to calculate revenue or find failed transactions.", "low", "SUPER_ADMIN")]
public record ListGlobalTransactionsAgentQuery(
    Guid OrganizationId,
    DateTime? FromDate,
    DateTime? ToDate,
    string? Status) : IQuery<IEnumerable<AgentTransactionResult>>;

public record AgentTransactionResult(
    string TransactionId,
    string CustomerName,
    string CustomerEmail,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string Status,
    string Date);

public class ListGlobalTransactionsAgentQueryHandler : IQueryHandler<ListGlobalTransactionsAgentQuery, IEnumerable<AgentTransactionResult>>
{
    private readonly ICommunityQueryService _queryService;
    private readonly ICrmQueryService _crmQueryService;

    public ListGlobalTransactionsAgentQueryHandler(
        ICommunityQueryService queryService,
        ICrmQueryService crmQueryService)
    {
        _queryService = queryService;
        _crmQueryService = crmQueryService;
    }

    public async Task<IEnumerable<AgentTransactionResult>> Handle(ListGlobalTransactionsAgentQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _queryService.GetGlobalTransactionsAsync(
            request.OrganizationId,
            request.FromDate,
            request.ToDate,
            request.Status);

        var transactionList = transactions.ToList();
        if (!transactionList.Any())
        {
            return Enumerable.Empty<AgentTransactionResult>();
        }

        var profileIds = transactionList.Select(t => t.ClientProfileId).Distinct();
        var profiles = await _crmQueryService.GetClientProfilesAsync(profileIds);
        var profileDict = profiles.ToDictionary(p => p.Id);

        return transactionList.Select(t =>
        {
            profileDict.TryGetValue(t.ClientProfileId, out var profile);
            return new AgentTransactionResult(
                t.Id.ToString(),
                profile?.FullName ?? "Unknown",
                profile?.Email ?? "Unknown",
                t.Amount,
                t.Currency,
                t.PaymentMethod,
                t.Status,
                t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }).ToList();
    }
}
