using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class TestRailTests
{
    [Test]
    public async Task Mint_and_start_pays_without_keys()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "test");

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK), await started.Content.ReadAsStringAsync());
        using var startDoc = JsonDocument.Parse(await started.Content.ReadAsStringAsync());
        Assert.That(startDoc.RootElement.GetProperty("redirect_url").GetString(), Does.Contain("status=verifying"));

        var get = await client.GetAsync($"/v1/pay/{token}");
        using var pay = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(pay.RootElement.GetProperty("status").GetString(), Is.EqualTo("paid"));
        Assert.That(pay.RootElement.GetProperty("provider").GetString(), Is.EqualTo("test"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Checkouts.Single(x => x.Id == checkoutId).Status, Is.EqualTo("paid"));
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Single().Title, Is.EqualTo("Official Receipt"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Webhook_pays_open_test_checkout()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(
                $$"""{"id":"evt_test_1","checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""",
                Encoding.UTF8,
                "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }
}
