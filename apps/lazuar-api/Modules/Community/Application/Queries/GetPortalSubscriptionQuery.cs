using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Community.Application.Queries;

public record GetPortalSubscriptionQuery(Guid OrganizationId, Guid SubscriptionId)
    : IQuery<CommunitySubscriptionDto?>;

public class GetPortalSubscriptionQueryHandler : IQueryHandler<GetPortalSubscriptionQuery, CommunitySubscriptionDto?>
{
    private readonly ICommunityQueryService _queryService;

    public GetPortalSubscriptionQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<CommunitySubscriptionDto?> Handle(GetPortalSubscriptionQuery request, CancellationToken ct)
    {
        return await _queryService.GetPortalSubscriptionAsync(request.OrganizationId, request.SubscriptionId);
    }
}
