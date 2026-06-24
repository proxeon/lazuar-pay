using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Application.Queries;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("Query the global financial ledger. Filter by date range, status (e.g., CONFIRMED, REFUNDED), or payment method to calculate revenue or find failed transactions.", "COMMUNITY", "low", "SUPER_ADMIN")]
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

    public ListGlobalTransactionsAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentTransactionResult>> Handle(ListGlobalTransactionsAgentQuery request, CancellationToken cancellationToken)
    {
        var response = await _queryService.GetGlobalTransactionsAsync(
            request.OrganizationId,
            page: 1,
            limit: 500,
            status: request.Status,
            paymentMethod: null,
            fromDate: request.FromDate,
            toDate: request.ToDate);

        var transactions = response.Data.ToList();
        if (!transactions.Any())
        {
            return Enumerable.Empty<AgentTransactionResult>();
        }

        return transactions.Select(t => new AgentTransactionResult(
            t.Id,
            t.Customer_name,
            t.Customer_email,
            (decimal)t.Amount,
            t.Currency,
            t.Payment_method,
            t.Status,
            t.Created_at.ToString("yyyy-MM-dd HH:mm:ss")
        )).ToList();
    }
}
