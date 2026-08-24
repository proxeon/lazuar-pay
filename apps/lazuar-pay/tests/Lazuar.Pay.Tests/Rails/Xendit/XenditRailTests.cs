using System.Net;
using System.Text;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class XenditRailTests
{
    [Test]
    public async Task Xendit_paid_and_settled()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"inv_1","invoice_url":"https://checkout.xendit.co/inv_1"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"xendit","secret":"xnd_sk","webhook_secret":"tok_1"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());

        var payload = "{\"id\":\"inv_1\",\"status\":\"PAID\",\"currency\":\"MYR\",\"paid_amount\":10,\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}";
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/xendit/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("x-callback-token", "tok_1");
        var paid = await client.SendAsync(wh);
        Assert.That(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());

        var settled = "{\"id\":\"inv_1\",\"status\":\"SETTLED\",\"currency\":\"MYR\",\"paid_amount\":10,\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}";
        using var wh2 = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/xendit/t1")
        {
            Content = new StringContent(settled, Encoding.UTF8, "application/json")
        };
        wh2.Headers.TryAddWithoutValidation("x-callback-token", "tok_1");
        var second = await client.SendAsync(wh2);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await second.Content.ReadAsStringAsync(), Does.Contain("settled").Or.Contain("ignored"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
    }

    [Test]
    public async Task Xendit_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"xendit","secret":"xnd","webhook_secret":"tok"}""");
        var (token, _) = await PayTest.SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Xendit_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"xendit","secret":"xnd","webhook_secret":"tok"}""");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/xendit/t1")
        {
            Content = new StringContent("  ", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
