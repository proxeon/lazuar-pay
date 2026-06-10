using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("List a subscriber's payment transaction history to find a PaymentRecordId required for processing refunds.", "low", "SUPER_ADMIN", "ADMIN")]
public record ListSubscriberPaymentsAgentQuery(Guid OrganizationId, Guid SubscriptionId) : IQuery<IEnumerable<AgentPaymentResult>>;

public record AgentPaymentResult(
    string PaymentRecordId, 
    double Amount, 
    string Currency, 
    string Status, 
    string Date, 
    string? ReferenceNumber);

public class ListSubscriberPaymentsAgentQueryHandler : IQueryHandler<ListSubscriberPaymentsAgentQuery, IEnumerable<AgentPaymentResult>>
{
    private readonly ICommunityQueryService _queryService;

    public ListSubscriberPaymentsAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentPaymentResult>> Handle(ListSubscriberPaymentsAgentQuery request, CancellationToken cancellationToken)
    {
        var response = await _queryService.GetPaymentHistoryAsync(request.OrganizationId, request.SubscriptionId, 1, 50);
        
        return response.Data.Select(p => new AgentPaymentResult(
            p.Id,
            p.Amount,
            p.Currency,
            p.Status,
            p.Created_at.ToString("yyyy-MM-dd HH:mm:ss"),
            p.Reference_number
        )).ToList();
    }
}
