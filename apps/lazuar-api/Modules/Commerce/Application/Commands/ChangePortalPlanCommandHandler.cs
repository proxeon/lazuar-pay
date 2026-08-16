using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Commands;
using Modules.One.Contracts;

namespace Modules.Commerce.Application.Commands;

public class ChangePortalPlanCommandHandler : ICommandHandler<ChangePortalPlanCommand, PlanChangePreview>
{
    private readonly ICommerceRepository _repository;
    private readonly IOneQueryService _oneQueryService;
    private readonly IMagicLinkTokenService _tokenService;

    public ChangePortalPlanCommandHandler(
        ICommerceRepository repository,
        IOneQueryService oneQueryService,
        IMagicLinkTokenService tokenService)
    {
        _repository = repository;
        _oneQueryService = oneQueryService;
        _tokenService = tokenService;
    }

    public async Task<PlanChangePreview> Handle(ChangePortalPlanCommand request, CancellationToken ct)
    {
        PlanChangePolicy.RejectImmediateOrProrate(request.Prorate, request.Apply);

        var subscription = await PortalSubscriptionAccess.ResolveOwnedAsync(
            _oneQueryService,
            _tokenService,
            _repository,
            request.TenantSlug,
            request.Token,
            request.SubscriptionId,
            ct);

        if (subscription.Status == "PAST_DUE")
        {
            throw new InvalidOperationException("Update payment first before changing plan.");
        }

        if (subscription.CancelAtPeriodEnd)
        {
            throw new InvalidOperationException("Keep the current plan before scheduling a different product.");
        }

        PlanChangePolicy.GuardLiveStatus(subscription);

        var current = await _repository.GetProductByIdAsync(subscription.ProductId, ct)
            ?? throw new InvalidOperationException("Associated product catalog entry not found.");

        if (request.ProductId is null || request.ProductId == subscription.ProductId)
        {
            subscription.ClearPendingPlanChange();
            await _repository.SaveChangesAsync(ct);
            return PlanChangePolicy.Preview(subscription, current, current, subscription.Quantity);
        }

        var target = await _repository.GetProductByIdAsync(request.ProductId.Value, ct);
        if (target == null || target.OrganizationId != subscription.OrganizationId)
        {
            throw new InvalidOperationException("Target product not found.");
        }

        PlanChangePolicy.GuardTargetProduct(subscription, current, target);
        subscription.SchedulePlanChange(target.Id);
        await _repository.SaveChangesAsync(ct);
        return PlanChangePolicy.Preview(subscription, current, target, subscription.PendingQuantity ?? subscription.Quantity);
    }
}
