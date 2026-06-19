using BuildingBlocks.Application;
using Modules.CRM.Contracts;

namespace Modules.Community.Application.Commands;

public record RequestMagicLinkCommand(
    Guid OrganizationId,
    string TenantSlug,
    string Email,
    string BaseUrl) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RequestMagicLinkCommandHandler : ICommandHandler<RequestMagicLinkCommand>
{
    private readonly ICrmQueryService _crmQueryService;
    private readonly ICommunitySubscriptionRepository _repository;
    private readonly IMagicLinkTokenService _tokenService;

    public RequestMagicLinkCommandHandler(
        ICrmQueryService crmQueryService,
        ICommunitySubscriptionRepository repository,
        IMagicLinkTokenService tokenService)
    {
        _crmQueryService = crmQueryService;
        _repository = repository;
        _tokenService = tokenService;
    }

    public async Task Handle(RequestMagicLinkCommand request, CancellationToken ct)
    {
        var profile = await _crmQueryService.GetClientProfileByEmailAsync(request.OrganizationId, request.Email);
        if (profile == null) return; 

        var subscription = await _repository.GetActiveByProfileIdAsync(request.OrganizationId, Guid.Parse(profile.Id), ct);
        if (subscription == null) return; 

        var token = _tokenService.GenerateToken(subscription.Id);
        var magicLinkUrl = $"{request.BaseUrl.TrimEnd('/')}/{request.TenantSlug}/portal?token={Uri.EscapeDataString(token)}";

        subscription.RequestMagicLink(magicLinkUrl);

        await _repository.SaveChangesAsync(ct);
    }
}
