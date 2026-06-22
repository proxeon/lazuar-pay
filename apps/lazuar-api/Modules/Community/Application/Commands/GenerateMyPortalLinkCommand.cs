using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.CRM.Contracts;
using Modules.One.Contracts;

namespace Modules.Community.Application.Commands;

public record GenerateMyPortalLinkCommand(Guid UserId, Guid SubscriptionId) : ICommand<GeneratePortalLinkResponseDto>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class GenerateMyPortalLinkCommandHandler : ICommandHandler<GenerateMyPortalLinkCommand, GeneratePortalLinkResponseDto>
{
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IOneQueryService _oneQueryService;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly ICommunityLinkService _linkService;

    public GenerateMyPortalLinkCommandHandler(
        ICommunitySubscriptionRepository subscriptionRepository,
        ICrmQueryService crmQueryService,
        IOneQueryService oneQueryService,
        IMagicLinkTokenService tokenService,
        ICommunityLinkService linkService)
    {
        _subscriptionRepository = subscriptionRepository;
        _crmQueryService = crmQueryService;
        _oneQueryService = oneQueryService;
        _tokenService = tokenService;
        _linkService = linkService;
    }

    public async Task<GeneratePortalLinkResponseDto> Handle(GenerateMyPortalLinkCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null)
            throw new InvalidOperationException("Subscription not found.");

        var profile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
        if (profile == null || profile.Global_user_id != request.UserId.ToString())
            throw new InvalidOperationException("Unauthorized access to this subscription.");

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(subscription.OrganizationId);
        if (workspace == null)
            throw new InvalidOperationException("Workspace not found.");

        var token = _tokenService.GenerateToken(subscription.Id);
        var baseUrl = _linkService.GetCommunityBaseUrl().TrimEnd('/');
        var portalUrl = $"{baseUrl}/{workspace.Slug}/portal?token={Uri.EscapeDataString(token)}";

        return new GeneratePortalLinkResponseDto { Url = portalUrl };
    }
}
