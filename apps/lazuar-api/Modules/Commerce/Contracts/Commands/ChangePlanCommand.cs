using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record PlanChangePreview(
    Guid CurrentProductId,
    decimal CurrentAmount,
    string Currency,
    string Interval,
    Guid NextProductId,
    decimal NextAmount,
    DateTime? EffectiveAt,
    decimal AmountDueNow,
    string Policy);

public record ChangePlanCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid? ProductId,
    bool? Prorate = null,
    string? Apply = null) : ICommand<PlanChangePreview>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record SetSubscriptionQuantityCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    int Quantity,
    bool? Prorate = null,
    string? Apply = null) : ICommand<PlanChangePreview>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record PauseCollectionCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    DateTime ResumeOn) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record ResumeCollectionCommand(
    Guid OrganizationId,
    Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record ChangePortalPlanCommand(
    string TenantSlug,
    string Token,
    Guid SubscriptionId,
    Guid? ProductId,
    bool? Prorate = null,
    string? Apply = null) : ICommand<PlanChangePreview>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
