using System;
using FluentAssertions;
using Modules.Commerce.Application;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceCheckoutIdempotencyTests
{
    [Test]
    public void Normalize_Empty_Is_Null()
    {
        CommerceCheckoutIdempotency.NormalizeKey(null).Should().BeNull();
        CommerceCheckoutIdempotency.NormalizeKey("  ").Should().BeNull();
    }

    [Test]
    public void Normalize_Rejects_Over_200()
    {
        var act = () => CommerceCheckoutIdempotency.NormalizeKey(new string('k', 201));
        act.Should().Throw<InvalidOperationException>().WithMessage("*200*");
    }

    [Test]
    public void Fingerprint_Changes_When_Product_Changes()
    {
        var org = Guid.CreateVersion7();
        var a = CommerceCheckoutIdempotency.Fingerprint(org, "plan-a", "a@x.com", null, 1, null);
        var b = CommerceCheckoutIdempotency.Fingerprint(org, "plan-b", "a@x.com", null, 1, null);
        a.Should().NotBe(b);
        a.Should().HaveLength(64);
    }

    [Test]
    public void TryReplayUrl_OnlyOpenUnexpiredWithUrl()
    {
        var org = Guid.CreateVersion7();
        var open = new Modules.Commerce.Domain.Aggregates.CheckoutSession(
            org, Guid.CreateVersion7(), Guid.CreateVersion7(), null, DateTime.UtcNow.AddHours(1));
        open.SetGatewayCheckoutUrl("https://pay.example/hop2");

        CommerceCheckoutIdempotency.TryReplayUrl(open, DateTime.UtcNow, out var url).Should().BeTrue();
        url.Should().Be("https://pay.example/hop2");

        open.Expire();
        CommerceCheckoutIdempotency.TryReplayUrl(open, DateTime.UtcNow, out _).Should().BeFalse();
        CommerceCheckoutIdempotency.ShouldReleaseKey(open, DateTime.UtcNow).Should().BeTrue();
    }

    [Test]
    public void TryReplayUrl_OpenWithoutUrl_IsNotReplay()
    {
        var open = new Modules.Commerce.Domain.Aggregates.CheckoutSession(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), null, DateTime.UtcNow.AddHours(1));
        CommerceCheckoutIdempotency.TryReplayUrl(open, DateTime.UtcNow, out _).Should().BeFalse();
        CommerceCheckoutIdempotency.IsReplayableOpen(open, DateTime.UtcNow).Should().BeTrue();
    }
}
