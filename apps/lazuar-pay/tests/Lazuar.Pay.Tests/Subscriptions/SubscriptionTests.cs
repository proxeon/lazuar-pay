using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// plans/031/01 (Option A): recurring billing is not offered — a `mo`/`yr` checkout bills
/// exactly once while the subscriptions row would claim otherwise. The intervals are
/// refused at every entry point, no subscription rows are ever minted, and the list
/// endpoint reads the (legacy/future) table.
/// </summary>
public class SubscriptionTests
{
    static async Task<HttpResponseMessage> Post(HttpClient client, string path, string json)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        return await client.SendAsync(req);
    }

    [Test]
    public async Task Recurring_intervals_are_refused_at_every_entry_point()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();

        foreach (var interval in new[] { "mo", "yr" })
        {
            var checkout = await Post(client, "/v1/checkouts",
                $$"""{"org_id":"t1","amount":10,"provider":"test","interval":"{{interval}}"}""");
            Assert.That(checkout.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), interval);
            Assert.That(await checkout.Content.ReadAsStringAsync(), Does.Contain("recurring billing is not offered"));

            var product = await Post(client, "/v1/orgs/t1/products",
                $$"""{"name":"B","amount":10,"currency":"MYR","interval":"{{interval}}"}""");
            Assert.That(product.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), interval);
            Assert.That(await product.Content.ReadAsStringAsync(), Does.Contain("recurring billing is not offered"));
        }
    }

    [Test]
    public async Task One_off_checkout_mints_no_subscription_rows()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var minted = await client.SendAsync(create);
        Assert.That(minted.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var mintedDoc = JsonDocument.Parse(await minted.Content.ReadAsStringAsync());
        Assert.That(mintedDoc.RootElement.GetProperty("interval").GetString(), Is.EqualTo("one_off"));
        var token = mintedDoc.RootElement.GetProperty("public_token").GetString();

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/subscriptions");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        using var doc = JsonDocument.Parse(await (await client.SendAsync(list)).Content.ReadAsStringAsync());
        Assert.That(PayTest.Items(doc.RootElement).EnumerateArray(), Is.Empty);
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Subscriptions.Count(), Is.EqualTo(0));
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().OrgWebhookDeliveries
            .Any(x => x.EventType.StartsWith("subscription.")), Is.False);
    }
}
