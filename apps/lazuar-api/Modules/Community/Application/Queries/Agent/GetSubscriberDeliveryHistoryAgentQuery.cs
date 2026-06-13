using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("Audit the delivery history of emails and WhatsApp messages sent to a subscriber to verify success or bounce errors.", "COMMUNITY", "low", "SUPER_ADMIN", "ADMIN")]
public record GetSubscriberDeliveryHistoryAgentQuery(Guid OrganizationId, Guid SubscriptionId) : IQuery<IEnumerable<AgentDeliveryResult>>;

public record AgentDeliveryResult(
    string DeliveryId,
    string Channel,
    string? TemplateName,
    string Status,
    string Date,
    string? ErrorMessage);

public class GetSubscriberDeliveryHistoryAgentQueryHandler : IQueryHandler<GetSubscriberDeliveryHistoryAgentQuery, IEnumerable<AgentDeliveryResult>>
{
    private readonly ICommunityQueryService _queryService;

    public GetSubscriberDeliveryHistoryAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentDeliveryResult>> Handle(GetSubscriberDeliveryHistoryAgentQuery request, CancellationToken cancellationToken)
    {
        var history = await _queryService.GetReminderHistoryAsync(request.OrganizationId, request.SubscriptionId);

        return history.Select(h => new AgentDeliveryResult(
            h.Id,
            h.Channel,
            h.Template_name,
            h.Status,
            h.Created_at.ToString("yyyy-MM-dd HH:mm:ss"),
            h.Error_message
        )).ToList();
    }
}
