using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application.Commands;

public class ChangePlanCommandHandler : ICommandHandler<ChangePlanCommand, PlanChangePreview>
{
    private readonly ICommerceRepository _repository;

    public ChangePlanCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task<PlanChangePreview> Handle(ChangePlanCommand request, CancellationToken ct)
    {
        PlanChangePolicy.RejectImmediateOrProrate(request.Prorate, request.Apply);

        var subscription = await _repository.GetSubscriptionByIdAsync(request.OrganizationId, request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        PlanChangePolicy.GuardLiveStatus(subscription);

        var current = await _repository.GetProductByIdAsync(subscription.OrganizationId, subscription.ProductId, ct)
            ?? throw new InvalidOperationException("Associated product catalog entry not found.");

        if (request.ProductId is null || request.ProductId == subscription.ProductId)
        {
            subscription.ClearPendingPlanChange();
            await _repository.SaveChangesAsync(ct);
            return PlanChangePolicy.Preview(subscription, current, current, subscription.Quantity);
        }

        var target = await _repository.GetProductByIdAsync(request.OrganizationId, request.ProductId.Value, ct);
        if (target == null || target.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Target product not found.");
        }

        PlanChangePolicy.GuardTargetProduct(subscription, current, target);
        subscription.SchedulePlanChange(target.Id);
        await _repository.SaveChangesAsync(ct);
        return PlanChangePolicy.Preview(subscription, current, target, subscription.PendingQuantity ?? subscription.Quantity);
    }
}

public class SetSubscriptionQuantityCommandHandler : ICommandHandler<SetSubscriptionQuantityCommand, PlanChangePreview>
{
    private readonly ICommerceRepository _repository;

    public SetSubscriptionQuantityCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task<PlanChangePreview> Handle(SetSubscriptionQuantityCommand request, CancellationToken ct)
    {
        PlanChangePolicy.RejectImmediateOrProrate(request.Prorate, request.Apply);

        var subscription = await _repository.GetSubscriptionByIdAsync(request.OrganizationId, request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        PlanChangePolicy.GuardLiveStatus(subscription);
        subscription.ScheduleQuantity(request.Quantity);

        var current = await _repository.GetProductByIdAsync(subscription.OrganizationId, subscription.ProductId, ct)
            ?? throw new InvalidOperationException("Associated product catalog entry not found.");
        Product target = current;
        if (subscription.PendingProductId.HasValue)
        {
            target = await _repository.GetProductByIdAsync(subscription.OrganizationId, subscription.PendingProductId.Value, ct) ?? current;
        }

        await _repository.SaveChangesAsync(ct);
        return PlanChangePolicy.Preview(subscription, current, target, request.Quantity);
    }
}

public class PauseCollectionCommandHandler : ICommandHandler<PauseCollectionCommand>
{
    private readonly ICommerceRepository _repository;

    public PauseCollectionCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(PauseCollectionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.OrganizationId, request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        subscription.PauseCollection(request.ResumeOn);
        await _repository.SaveChangesAsync(ct);
    }
}

public class ResumeCollectionCommandHandler : ICommandHandler<ResumeCollectionCommand>
{
    private readonly ICommerceRepository _repository;

    public ResumeCollectionCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ResumeCollectionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.OrganizationId, request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        DateTime? nextBill = null;
        if (subscription.NextBillingDate == null || subscription.NextBillingDate < DateTime.UtcNow)
        {
            var product = await _repository.GetProductByIdAsync(subscription.OrganizationId, subscription.ProductId, ct);
            var interval = SubscriptionBillingAmount.ResolveInterval(subscription, product ?? throw new InvalidOperationException("Associated product catalog entry not found."));
            nextBill = SubscriptionBillingAmount.AdvanceFrom(DateTime.UtcNow, interval);
        }

        subscription.ResumeCollection(nextBill);
        await _repository.SaveChangesAsync(ct);
    }
}
