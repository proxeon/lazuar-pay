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
}
