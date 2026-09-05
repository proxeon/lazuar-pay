using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// plans/031/01 (Option A): with recurring billing not offered, the issue-004 failed→open
/// re-open path is gone — a failed checkout is terminal (a fresh checkout is the retry),
/// even when a legacy subscription row from before the removal still points at it.
/// </summary>
public class DunningRecoveryTests
{
    static async Task<string> CreateCheckout(HttpClient client)
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var minted = await client.SendAsync(create);
        Assert.That(minted.StatusCode, Is.EqualTo(HttpStatusCode.Created), await minted.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await minted.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("public_token").GetString()!;
    }

    static async Task<string> Fail(HttpClient client, string token)
    {
        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/checkouts?limit=1");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        using var listDoc = JsonDocument.Parse(await (await client.SendAsync(list)).Content.ReadAsStringAsync());
        var checkoutId = PayTest.Items(listDoc.RootElement)[0].GetProperty("id").GetString()!;

        var body = $$"""{"id":"evt_fail_{{Guid.NewGuid():N}}","checkout_id":"{{checkoutId}}","status":"failed","currency":"myr"}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return checkoutId;
    }

    [Test]
    public async Task Failed_checkout_is_terminal_even_with_a_legacy_subscription_row()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var token = await CreateCheckout(client);
        var checkoutId = await Fail(client, token);

        // A row left behind by a deployment that predates the interval removal must not
        // resurrect the checkout.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Subscriptions.Add(new SubscriptionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = "t1",
                CheckoutId = checkoutId,
                Status = "past_due",
                Interval = "mo",
                AttemptCount = 1,
                PastDueAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            Assert.That(db.Checkouts.Single().Status, Is.EqualTo("failed"));
        }

        using var retry = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(retry);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict),
            "a legacy subscription row must not re-open a failed checkout");
    }
}
