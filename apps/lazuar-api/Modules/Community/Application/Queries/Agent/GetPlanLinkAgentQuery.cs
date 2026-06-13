using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Contracts;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("Generate the public, shareable URL for a specific subscription plan. Use this when the user wants a link to send to their customers.", "COMMUNITY", "low", "SUPER_ADMIN", "ADMIN")]
public record GetPlanLinkAgentQuery(Guid OrganizationId, Guid PlanId) : IQuery<string>;

public class GetPlanLinkAgentQueryHandler : IQueryHandler<GetPlanLinkAgentQuery, string>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly IOneQueryService _oneQueryService;
    private readonly ICommunityLinkService _linkService;

    public GetPlanLinkAgentQueryHandler(
        ICommunityPlanRepository planRepository,
        IOneQueryService oneQueryService,
        ICommunityLinkService linkService)
    {
        _planRepository = planRepository;
        _oneQueryService = oneQueryService;
        _linkService = linkService;
    }

    public async Task<string> Handle(GetPlanLinkAgentQuery request, CancellationToken cancellationToken)
    {
        // 1. Validate the Plan belongs to this tenant and get its Slug
        var plan = await _planRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null || plan.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Plan not found in the current workspace.");
        }

        // 2. Fetch the Tenant Slug via cross-module query to the One module
        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(request.OrganizationId);
        if (workspace == null)
        {
            throw new InvalidOperationException("Workspace context invalid.");
        }

        // 3. Resolve environment-aware base URL (localhost:3021 vs community.lazuar.com)
        var baseUrl = _linkService.GetCommunityBaseUrl().TrimEnd('/');

        // 4. Construct final public URL
        return $"{baseUrl}/{workspace.Slug}/{plan.Slug}";
    }
}
