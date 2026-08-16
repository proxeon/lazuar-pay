using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class ChangePortalPlanCommandHandlerTests
{
    [Test]
    public async Task TokenCannotChangeAnotherClientsSubscription()
    {
        var fx = Arrange();
        var otherClient = Guid.CreateVersion7();
        var foreign = new Subscription(fx.OrgId, otherClient, fx.Current.Id);
        foreign.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(14), false, 1, 40m);
        fx.Repo.GetSubscriptionByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        var act = () => fx.Handler.Handle(
            new ChangePortalPlanCommand("acme", "tok", foreign.Id, fx.Target.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*does not belong*");
        foreign.PendingProductId.Should().BeNull();
        foreign.ProductId.Should().Be(fx.Current.Id);
        await fx.Repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProrateTrue_Throws()
    {
        var fx = Arrange();
        var act = () => fx.Handler.Handle(
            new ChangePortalPlanCommand("acme", "tok", fx.Sub.Id, fx.Target.Id, Prorate: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Proration*");
        fx.Sub.PendingProductId.Should().BeNull();
        await fx.Repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static Fx Arrange()
    {
        var org = Guid.CreateVersion7();
        var client = Guid.CreateVersion7();
        var current = Product(org, "Basic", 40m);
        var target = Product(org, "Pro", 90m);
        var sub = new Subscription(org, client, current.Id);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(14), false, 1, 40m);

        var repo = Substitute.For<ICommerceRepository>();
        repo.GetSubscriptionByIdAsync(sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repo.GetProductByIdAsync(current.Id, Arg.Any<CancellationToken>()).Returns(current);
        repo.GetProductByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(org);

        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.ValidateToken("tok").Returns(sub.Id);

        return new Fx(org, sub, current, target, repo, new ChangePortalPlanCommandHandler(repo, one, tokens));
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
        ChangePortalPlanCommandHandler Handler);
}
