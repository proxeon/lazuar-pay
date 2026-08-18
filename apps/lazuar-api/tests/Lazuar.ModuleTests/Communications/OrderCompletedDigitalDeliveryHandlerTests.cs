using System;
using Modules.Communications.Infrastructure.EventHandlers;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class OrderCompletedDigitalDeliveryHandlerTests
{
    [Test]
    public void FirstHttpsFulfillment_Picks_First_Http_Url()
    {
        var url = OrderCompletedDigitalDeliveryHandler.FirstHttpsFulfillment(
        [
            "internal:vault/abc",
            "https://files.example.com/guide.pdf",
            "https://other.example/x"
        ]);

        Assert.That(url, Is.EqualTo("https://files.example.com/guide.pdf"));
    }

    [Test]
    public void FirstHttpsFulfillment_Returns_Null_Without_Http_Target()
    {
        Assert.That(OrderCompletedDigitalDeliveryHandler.FirstHttpsFulfillment(["internal:x", "not-a-url"]), Is.Null);
        Assert.That(OrderCompletedDigitalDeliveryHandler.FirstHttpsFulfillment([]), Is.Null);
        Assert.That(OrderCompletedDigitalDeliveryHandler.FirstHttpsFulfillment(null), Is.Null);
    }
}
