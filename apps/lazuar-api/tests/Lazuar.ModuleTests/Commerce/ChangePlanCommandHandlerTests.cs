using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class ChangePlanCommandHandlerTests
{
    [Test]
    public async Task Schedule_SetsPending_DoesNotMutateProductId()
    {
        var fx = Arrange();
        var preview = await fx.Handler.Handle(
            new ChangePlanCommand(fx.OrgId, fx.Sub.Id, fx.Target.Id),
            CancellationToken.None);

        fx.Sub.ProductId.Should().Be(fx.Current.Id);
        fx.Sub.PendingProductId.Should().Be(fx.Target.Id);
        fx.Sub.Status.Should().Be("ACTIVE");
        preview.AmountDueNow.Should().Be(0m);
        preview.Policy.Should().Be("next_renewal");
        await fx.Repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ClearPending_WhenProductIdNull()
    {
        var fx = Arrange();
        fx.Sub.SchedulePlanChange(fx.Target.Id);

        await fx.Handler.Handle(new ChangePlanCommand(fx.OrgId, fx.Sub.Id, null), CancellationToken.None);

        fx.Sub.PendingProductId.Should().BeNull();
        fx.Sub.ProductId.Should().Be(fx.Current.Id);
    }

    [Test]
    public async Task CancelAtPeriodEnd_Throws()
    {
        var fx = Arrange();
        fx.Sub.ScheduleCancelAtPeriodEnd();

        var act = () => fx.Handler.Handle(
            new ChangePlanCommand(fx.OrgId, fx.Sub.Id, fx.Target.Id),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Keep the current plan*");
        fx.Sub.PendingProductId.Should().BeNull();
    }

    [Test]
    public async Task ProrateTrue_Throws()
    {
        var fx = Arrange();
        var act = () => fx.Handler.Handle(
            new ChangePlanCommand(fx.OrgId, fx.Sub.Id, fx.Target.Id, Prorate: true),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Proration*");
    }

    [Test]
    public async Task ForeignOrgProduct_Throws()
    {
        var fx = Arrange();
        var foreign = Product(Guid.CreateVersion7(), "Other", 30m);
        fx.Repo.GetProductByIdAsync(Arg.Any<Guid>(), foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        var act = () => fx.Handler.Handle(
            new ChangePlanCommand(fx.OrgId, fx.Sub.Id, foreign.Id),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Test]
    public async Task OneTimeTarget_Throws()
    {
        var fx = Arrange();
        var oneTime = new Product(fx.OrgId, "Once", "once", 5m, "FIXED", 0m, "MYR", "one_time", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());
        fx.Repo.GetProductByIdAsync(Arg.Any<Guid>(), oneTime.Id, Arg.Any<CancellationToken>()).Returns(oneTime);

        var act = () => fx.Handler.Handle(
            new ChangePlanCommand(fx.OrgId, fx.Sub.Id, oneTime.Id),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*recurring*");
    }

    private static Fx Arrange()
    {
        var org = Guid.CreateVersion7();
        var current = Product(org, "Basic", 40m);
        var target = Product(org, "Pro", 90m);
        var sub = new Subscription(org, Guid.CreateVersion7(), current.Id);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(14), false, 1, 40m);

        var repo = Substitute.For<ICommerceRepository>();
        repo.GetSubscriptionByIdAsync(Arg.Any<Guid>(), sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repo.GetProductByIdAsync(Arg.Any<Guid>(), current.Id, Arg.Any<CancellationToken>()).Returns(current);
        repo.GetProductByIdAsync(Arg.Any<Guid>(), target.Id, Arg.Any<CancellationToken>()).Returns(target);

        return new Fx(org, sub, current, target, repo, new ChangePlanCommandHandler(repo));
    }

    private static Product Product(Guid org, string name, decimal price) =>
        new(org, name, name.ToLowerInvariant(), price, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());

    private sealed record Fx(
        Guid OrgId,
        Subscription Sub,
        Product Current,
        Product Target,
        ICommerceRepository Repo,
        ChangePlanCommandHandler Handler);
}
