using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Commands;
using Modules.One.Contracts;

namespace Modules.Commerce.Application.Commands;

public class KeepPortalSubscriptionCommandHandler : ICommandHandler<KeepPortalSubscriptionCommand>
{
    private readonly IOneQueryService _oneQueryService;
    private readonly IMagicLinkTokenService _tokenService;
    private readonly ICommerceRepository _repository;

    public KeepPortalSubscriptionCommandHandler(
        IOneQueryService oneQueryService,
        IMagicLinkTokenService tokenService,
        ICommerceRepository repository)
    {
        _oneQueryService = oneQueryService;
        _tokenService = tokenService;
        _repository = repository;
    }

    public async Task Handle(KeepPortalSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await PortalSubscriptionAccess.ResolveOwnedAsync(
            _oneQueryService,
            _tokenService,
            _repository,
            request.TenantSlug,
            request.Token,
            request.SubscriptionId,
            ct);

        if (subscription.Status == "CANCELED")
        {
            throw new InvalidOperationException("Subscription is already canceled.");
        }

        subscription.ClearScheduledCancel();
        await _repository.SaveChangesAsync(ct);
    }
}
