using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class SubscriptionCancelAtPeriodEndTests
{
    [Test]
    public void ScheduleCancelAtPeriodEnd_ActiveWithFuturePaidThrough_SetsFlagKeepsStatusAndDates()
    {
        var periodEnd = DateTime.UtcNow.AddDays(-1);
        var next = DateTime.UtcNow.AddDays(10);
        var sub = ActiveSub(nextBilling: next, periodEnd: periodEnd);

        sub.ScheduleCancelAtPeriodEnd();

        sub.CancelAtPeriodEnd.Should().BeTrue();
        sub.Status.Should().Be("ACTIVE");
        sub.NextBillingDate.Should().Be(next);
        sub.CurrentPeriodEnd.Should().Be(periodEnd);
    }

    [Test]
    public void ScheduleCancelAtPeriodEnd_PastDue_Throws()
    {
        var sub = ActiveSub(DateTime.UtcNow.AddDays(10));
        sub.MarkAsPastDue();

        var act = () => sub.ScheduleCancelAtPeriodEnd();

        act.Should().Throw<InvalidOperationException>().WithMessage("*PAST_DUE*");
        sub.CancelAtPeriodEnd.Should().BeFalse();
    }

    [Test]
    public void ScheduleCancelAtPeriodEnd_Suspended_Throws()
    {
        var sub = ActiveSub(DateTime.UtcNow.AddDays(10));
        sub.Suspend();

        var act = () => sub.ScheduleCancelAtPeriodEnd();

        act.Should().Throw<InvalidOperationException>().WithMessage("*SUSPENDED*");
    }

    [Test]
    public void ScheduleCancelAtPeriodEnd_DueDateInThePast_Throws()
    {
        var sub = ActiveSub(DateTime.UtcNow.AddMinutes(-1));

        var act = () => sub.ScheduleCancelAtPeriodEnd();

        act.Should().Throw<InvalidOperationException>().WithMessage("*paid period*");
    }

    [Test]
    public void ScheduleCancelAtPeriodEnd_NullNextBilling_Throws()
    {
        var sub = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow, nextBillingDate: null);

        var act = () => sub.ScheduleCancelAtPeriodEnd();

        act.Should().Throw<InvalidOperationException>().WithMessage("*paid period*");
    }

    [Test]
    public void Cancel_FromFlagged_SetsCanceledAndClearsFlag()
    {
        var sub = ActiveSub(DateTime.UtcNow.AddDays(10));
        sub.ScheduleCancelAtPeriodEnd();

        sub.Cancel();

        sub.Status.Should().Be("CANCELED");
        sub.CancelAtPeriodEnd.Should().BeFalse();
    }

    [Test]
    public void ClearScheduledCancel_ClearsFlagStaysActive()
    {
        var sub = ActiveSub(DateTime.UtcNow.AddDays(10));
        sub.ScheduleCancelAtPeriodEnd();

        sub.ClearScheduledCancel();

        sub.CancelAtPeriodEnd.Should().BeFalse();
        sub.Status.Should().Be("ACTIVE");
    }

    [Test]
    public void RecoverFromPayment_ClearsScheduledCancelFlag()
    {
        var sub = ActiveSub(DateTime.UtcNow.AddDays(10));
        sub.ScheduleCancelAtPeriodEnd();
        sub.MarkAsPastDue();

        sub.RecoverFromPayment(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        sub.Status.Should().Be("ACTIVE");
        sub.CancelAtPeriodEnd.Should().BeFalse();
    }

    [Test]
    public void Resume_ClearsScheduledCancelFlag()
    {
        var sub = ActiveSub(DateTime.UtcNow.AddDays(10));
        sub.ScheduleCancelAtPeriodEnd();
        sub.Suspend();

        sub.Resume(DateTime.UtcNow.AddMonths(1));

        sub.Status.Should().Be("ACTIVE");
        sub.CancelAtPeriodEnd.Should().BeFalse();
    }

    [Test]
    public async Task Admin_AtPeriodEndFalse_CancelsAndPublishesEvent()
    {
        var (orgId, product, sub, repository, eventBus) = ArrangeAdmin();
        var handler = new CancelAdminSubscriptionCommandHandler(repository, eventBus);

        var status = await handler.Handle(
            new CancelAdminSubscriptionCommand(orgId, sub.Id, AtPeriodEnd: false),
            CancellationToken.None);

        status.Should().Be("CANCELED");
        sub.Status.Should().Be("CANCELED");
        sub.CancelAtPeriodEnd.Should().BeFalse();
        await eventBus.Received(1).PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_AtPeriodEndTrue_SchedulesWithoutEvent()
    {
        var (orgId, _, sub, repository, eventBus) = ArrangeAdmin();
        var handler = new CancelAdminSubscriptionCommandHandler(repository, eventBus);

        var status = await handler.Handle(
            new CancelAdminSubscriptionCommand(orgId, sub.Id, AtPeriodEnd: true),
            CancellationToken.None);

        status.Should().Be("scheduled");
        sub.Status.Should().Be("ACTIVE");
        sub.CancelAtPeriodEnd.Should().BeTrue();
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_AtPeriodEndTrue_WhenDue_FallsBackToImmediate()
    {
        var (orgId, _, sub, repository, eventBus) = ArrangeAdmin(nextBilling: DateTime.UtcNow.AddMinutes(-1));
        var handler = new CancelAdminSubscriptionCommandHandler(repository, eventBus);

        var status = await handler.Handle(
            new CancelAdminSubscriptionCommand(orgId, sub.Id, AtPeriodEnd: true),
            CancellationToken.None);

        status.Should().Be("CANCELED");
        sub.Status.Should().Be("CANCELED");
        await eventBus.Received(1).PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
    }

    [Test]
    public async Task Admin_AlreadyFlagged_AtPeriodEndTrue_NoSecondEvent()
    {
        var (orgId, _, sub, repository, eventBus) = ArrangeAdmin();
        sub.ScheduleCancelAtPeriodEnd();
        var handler = new CancelAdminSubscriptionCommandHandler(repository, eventBus);

        var status = await handler.Handle(
            new CancelAdminSubscriptionCommand(orgId, sub.Id, AtPeriodEnd: true),
            CancellationToken.None);

        status.Should().Be("scheduled");
        sub.Status.Should().Be("ACTIVE");
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminKeep_ClearsFlagWithoutEvent()
    {
        var (orgId, _, sub, repository, eventBus) = ArrangeAdmin();
        sub.ScheduleCancelAtPeriodEnd();
        var handler = new KeepAdminSubscriptionCommandHandler(repository);

        await handler.Handle(new KeepAdminSubscriptionCommand(orgId, sub.Id), CancellationToken.None);

        sub.CancelAtPeriodEnd.Should().BeFalse();
        sub.Status.Should().Be("ACTIVE");
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminKeep_OnCanceled_Throws()
    {
        var (orgId, _, sub, repository, _) = ArrangeAdmin();
        sub.Cancel();
        var handler = new KeepAdminSubscriptionCommandHandler(repository);

        var act = async () => await handler.Handle(
            new KeepAdminSubscriptionCommand(orgId, sub.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already canceled*");
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Portal_Schedule_SameAsAdminAfterTokenChecks()
    {
        var (orgId, _, sub, repository, eventBus) = ArrangeAdmin();
        var (one, tokens) = ArrangePortal("acme", orgId, sub);
        var handler = new CancelPortalSubscriptionCommandHandler(one, tokens, repository, eventBus);

        var status = await handler.Handle(
            new CancelPortalSubscriptionCommand("acme", "tok", sub.Id, AtPeriodEnd: true),
            CancellationToken.None);

        status.Should().Be("scheduled");
        sub.Status.Should().Be("ACTIVE");
        sub.CancelAtPeriodEnd.Should().BeTrue();
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Portal_CannotScheduleAnotherClientsSubscription()
    {
        var (orgId, _, ownerSub, repository, eventBus) = ArrangeAdmin();
        var foreign = ActiveSub(DateTime.UtcNow.AddDays(10), orgId);
        repository.GetSubscriptionByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        var (one, tokens) = ArrangePortal("acme", orgId, ownerSub);
        var handler = new CancelPortalSubscriptionCommandHandler(one, tokens, repository, eventBus);

        var act = async () => await handler.Handle(
            new CancelPortalSubscriptionCommand("acme", "tok", foreign.Id, AtPeriodEnd: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*does not belong*");
        foreign.CancelAtPeriodEnd.Should().BeFalse();
        foreign.Status.Should().Be("ACTIVE");
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Subscription ActiveSub(DateTime nextBilling, Guid? orgId = null, DateTime? periodEnd = null)
    {
        var sub = new Subscription(orgId ?? Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(periodEnd ?? DateTime.UtcNow, nextBilling);
        return sub;
    }

    private static (Guid OrgId, Product Product, Subscription Sub, ICommerceRepository Repository, IEventBus EventBus)
        ArrangeAdmin(DateTime? nextBilling = null)
    {
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId, "Plan", "plan", 10m, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());
        var sub = new Subscription(orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow, nextBilling ?? DateTime.UtcNow.AddMonths(1));

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repository.GetProductByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        return (orgId, product, sub, repository, Substitute.For<IEventBus>());
    }

    private static (IOneQueryService One, IMagicLinkTokenService Tokens) ArrangePortal(
        string slug,
        Guid orgId,
        Subscription tokenSub)
    {
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync(slug).Returns(orgId);
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.ValidateToken("tok").Returns(tokenSub.Id);
        return (one, tokens);
    }
}
