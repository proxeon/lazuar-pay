using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class SubscriptionTests
{
    [Test]
    public async Task Mint_interval_lists_incomplete_then_active_on_pay()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test","interval":"mo"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var minted = await client.SendAsync(create);
        Assert.That(minted.StatusCode, Is.EqualTo(HttpStatusCode.Created), await minted.Content.ReadAsStringAsync());
        using var mintedDoc = JsonDocument.Parse(await minted.Content.ReadAsStringAsync());
        Assert.That(mintedDoc.RootElement.GetProperty("interval").GetString(), Is.EqualTo("mo"));
        var token = mintedDoc.RootElement.GetProperty("public_token").GetString();

        using var listOpen = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/subscriptions");
        listOpen.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var listed = await client.SendAsync(listOpen);
        using var openDoc = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        Assert.That(PayTest.Items(openDoc.RootElement)[0].GetProperty("status").GetString(), Is.EqualTo("incomplete"));
        Assert.That(openDoc.RootElement.ToString(), Does.Not.Contain("subscription.activated"));

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var listPaid = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/subscriptions");
        listPaid.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var paidList = await client.SendAsync(listPaid);
        using var paidDoc = JsonDocument.Parse(await paidList.Content.ReadAsStringAsync());
        Assert.That(PayTest.Items(paidDoc.RootElement)[0].GetProperty("status").GetString(), Is.EqualTo("active"));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().OrgWebhookDeliveries.Any(x => x.EventType.StartsWith("subscription.")), Is.False);
    }

    [Test]
    public async Task Failed_marks_past_due()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test","interval":"yr"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var minted = await client.SendAsync(create);
        using var mintedDoc = JsonDocument.Parse(await minted.Content.ReadAsStringAsync());
        var checkoutId = mintedDoc.RootElement.GetProperty("id").GetString();
        var body = $$"""{"id":"evt_sub_fail","checkout_id":"{{checkoutId}}","status":"failed","currency":"myr"}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = factory.Services.CreateScope();
        var sub = scope.ServiceProvider.GetRequiredService<PayDbContext>().Subscriptions.Single();
        Assert.That(sub.Status, Is.EqualTo("past_due"));
        Assert.That(sub.AttemptCount, Is.EqualTo(1));
        Assert.That(sub.PastDueAt, Is.Not.Null);
    }
}
