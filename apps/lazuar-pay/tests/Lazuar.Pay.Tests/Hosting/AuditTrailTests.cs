using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// plans/031/05: every audit row answers who acted and what changed. JWT writers carry
/// their One user id, machine keys carry the key's user id, webhook-driven events carry
/// "psp:&lt;provider&gt;", and detail snapshots are non-sensitive (secrets and raw payloads
/// never appear).
/// </summary>
public class AuditTrailTests
{
    static AuditEventRow Audit(PayApiFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PayDbContext>().AuditEvents.Single(x => x.Action == action);
    }

    [Test]
    public async Task Gateway_upsert_audit_carries_writer_actor_and_non_sensitive_detail()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");

        var row = Audit(factory, "gateway.credentials.upsert");
        Assert.That(row.Actor, Is.EqualTo("u1"), "the JWT writer's One user id");
        Assert.That(row.Detail, Does.Contain("\"provider\":\"stripe\""));
        Assert.That(row.Detail, Does.Contain("\"last4\":\"ummy\""));
        Assert.That(row.Detail, Does.Contain("\"environment\":\"test\""));
        Assert.That(row.Detail, Does.Contain("\"webhook_configured\":true"));
        Assert.That(row.Detail, Does.Not.Contain("sk_test_dummy"));
        Assert.That(row.Detail, Does.Not.Contain("whsec_test_local"));
    }

    [Test]
    public async Task Machine_key_writes_carry_the_key_actor()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Key;
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + PayTest.MachineKey);
        Assert.That((await client.SendAsync(req)).IsSuccessStatusCode);

        var row = Audit(factory, "gateway.credentials.upsert");
        Assert.That(row.Actor, Is.EqualTo("key-1"), "a machine key writes under its own whoami user id");
    }

    [Test]
    public async Task Fulfillment_stamps_the_provider_as_actor()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var row = Audit(factory, "checkout.paid");
        Assert.That(row.Actor, Is.EqualTo("psp:test"));
        Assert.That(row.Detail, Does.Contain("\"checkout_id\":\"" + checkoutId + "\""));
        Assert.That(row.Detail, Does.Contain("\"amount\":10"));
        Assert.That(row.Detail, Does.Contain("\"currency\":\"MYR\""));
    }

    [Test]
    public async Task Refund_create_and_resolve_audits_carry_actor_and_outcome()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Add(new CheckoutRow
            {
                Id = "c_aud", OrgId = "t1", PublicToken = "tok_c_aud",
                Amount = 10m, Currency = "MYR", Status = "paid", Provider = "test"
            });
            db.Charges.Add(new ChargeRow
            {
                Id = "ch_aud", OrgId = "t1", CheckoutId = "c_aud",
                Provider = "test", ProviderRef = "re_c_aud", Amount = 10m, Currency = "MYR", Status = "paid"
            });
            await db.SaveChangesAsync();
        }

        using var refund = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent("""{"checkout_id":"c_aud"}""", Encoding.UTF8, "application/json")
        };
        refund.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(refund)).StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = Audit(factory, "refund.created");
        Assert.That(created.Actor, Is.EqualTo("u1"));
        Assert.That(created.Detail, Does.Contain("\"refund_id\":"));
        Assert.That(created.Detail, Does.Contain("\"checkout_id\":\"c_aud\""));
        Assert.That(created.Detail, Does.Contain("\"amount\":10"));

        // The manual reconciliation exit (plans/031/02) — the decision needs a name on it.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Refunds.Add(new RefundRow
            {
                Id = "rf_manual", OrgId = "t1", CheckoutId = "c_aud", ChargeId = "ch_aud",
                Amount = 10m, Currency = "MYR", Status = "pending", Provider = "billplz",
                Reason = "late_pay", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var resolve = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds/rf_manual/resolve")
        {
            Content = new StringContent("""{"status":"succeeded"}""", Encoding.UTF8, "application/json")
        };
        resolve.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(resolve)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var resolved = Audit(factory, "refund.resolved");
        Assert.That(resolved.Actor, Is.EqualTo("u1"));
        Assert.That(resolved.Detail, Does.Contain("\"refund_id\":\"rf_manual\""));
        Assert.That(resolved.Detail, Does.Contain("\"status\":\"succeeded\""));
    }

    [Test]
    public async Task Webhook_rotate_is_audited_with_url_and_prefix_never_the_secret()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/webhooks")
        {
            Content = new StringContent("""{"url":"http://127.0.0.1:9/hook"}""", Encoding.UTF8, "application/json")
        };
        put.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(put)).IsSuccessStatusCode);

        using var rotate = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/webhooks/rotate")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        rotate.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var rotated = await client.SendAsync(rotate);
        Assert.That(rotated.IsSuccessStatusCode, await rotated.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync());
        var prefix = doc.RootElement.GetProperty("secret_prefix").GetString()!;
        var secret = doc.RootElement.GetProperty("webhook_secret").GetString()!;

        var rotatedRow = Audit(factory, "org.webhook.rotated");
        Assert.That(rotatedRow.Actor, Is.EqualTo("u1"));
        Assert.That(rotatedRow.Detail, Does.Contain("127.0.0.1:9/hook"));
        Assert.That(rotatedRow.Detail, Does.Contain(prefix));
        Assert.That(rotatedRow.Detail, Does.Not.Contain(secret));

        var upsertRow = Audit(factory, "org.webhook.upsert");
        Assert.That(upsertRow.Actor, Is.EqualTo("u1"));
        Assert.That(upsertRow.Detail, Does.Contain("127.0.0.1:9/hook"));
    }
}
