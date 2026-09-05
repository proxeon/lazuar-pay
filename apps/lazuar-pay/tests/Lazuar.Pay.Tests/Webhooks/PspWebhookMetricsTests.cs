using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// plans/031/04: every delivered webhook event records exactly one
/// psp_parse_outcome{provider,outcome} measurement — the contract-drift signal. The
/// listener below captures the same counter an OTel exporter would, and the tests walk one
/// fixture per outcome through the real Handle pipeline (signature, binding, amounts,
/// ignore persistence).
/// </summary>
public class PspWebhookMetricsTests
{
    private sealed class OutcomeRecorder : IDisposable
    {
        public List<(string Provider, string Outcome)> Recorded { get; } = [];

        private readonly MeterListener _listener = new();

        public OutcomeRecorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Lazuar.Pay.Webhooks" && instrument.Name == "psp_parse_outcome")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
            {
                string provider = "";
                string outcome = "";
                foreach (var tag in tags)
                {
                    if (tag.Key == "provider") provider = tag.Value?.ToString() ?? "";
                    else if (tag.Key == "outcome") outcome = tag.Value?.ToString() ?? "";
                }

                Recorded.Add((provider, outcome));
            });
            _listener.Start();
        }

        public int Count(string provider, string outcome) =>
            Recorded.Count(x => x.Provider == provider && x.Outcome == outcome);

        public void Dispose() => _listener.Dispose();
    }

    static string Sign(string secret, string payload, long t)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{t}.{payload}"));
        return $"t={t},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    static async Task<HttpResponseMessage> PostStripe(
        HttpClient client, string payload, string? secret = "whsec_test_local", string orgId = "t1")
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/webhooks/stripe/{orgId}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (secret is not null)
        {
            var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            req.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(secret, payload, t));
        }
        return await client.SendAsync(req);
    }

    static string StripePayload(string checkoutId, string sessionId, string eventId, string amount = "1000", string currency = "myr", bool withBinding = true)
    {
        var binding = withBinding
            ? $",\"client_reference_id\":\"{checkoutId}\",\"metadata\":{{\"checkout_id\":\"{checkoutId}\"}}"
            : "";
        return "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"" + sessionId + "\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":" + amount + ",\"currency\":\"" + currency + "\",\"payment_status\":\"paid\",\"status\":\"complete\"" + binding + "}}}";
    }

    [Test]
    public async Task Paid_event_counts_ok_redelivery_counts_dedupe_and_bad_signature_counts_verify_failed()
    {
        using var recorder = new OutcomeRecorder();
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);
        var payload = StripePayload(checkoutId, "cs_ok_1", "evt_ok_1");

        var paid = await PostStripe(client, payload);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(recorder.Count("stripe", "ok"), Is.EqualTo(1));

        var replay = await PostStripe(client, payload);
        Assert.That(await replay.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
        Assert.That(recorder.Count("stripe", "dedupe"), Is.EqualTo(1));

        var unsigned = await PostStripe(client, StripePayload(checkoutId, "cs_ok_2", "evt_ok_2"), secret: null);
        Assert.That(unsigned.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await unsigned.Content.ReadAsStringAsync(), Does.Contain("invalid signature"));
        Assert.That(recorder.Count("stripe", "verify_failed"), Is.EqualTo(1));

        // The unconfigured-rail path answers the same anti-oracle "invalid signature"
        // (002/013) — posted to an org with no vaulted stripe key.
        var unconfigured = await PostStripe(client, StripePayload(checkoutId, "cs_ok_3", "evt_ok_3"), orgId: "t9");
        Assert.That(unconfigured.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(recorder.Count("stripe", "verify_failed"), Is.EqualTo(2));
    }

    [Test]
    public async Task Binding_and_unit_mismatches_count_as_drift()
    {
        using var recorder = new OutcomeRecorder();
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);

        // Binding fields gone (no client_reference_id, no metadata, unknown session) — the
        // classic symptom of a vendor changing its event shape.
        var unbound = await PostStripe(client, StripePayload(checkoutId, "cs_unknown", "evt_bind_1", withBinding: false));
        Assert.That(unbound.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await unbound.Content.ReadAsStringAsync(), Does.Contain("checkout not found"));
        Assert.That(recorder.Count("stripe", "checkout_missing"), Is.EqualTo(1));

        // Currency drift: the event now settles in a currency the checkout never quoted.
        var currency = await PostStripe(client, StripePayload(checkoutId, "cs_bind_2", "evt_bind_2", currency: "usd"));
        Assert.That(currency.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await currency.Content.ReadAsStringAsync(), Does.Contain("currency mismatch"));
        Assert.That(recorder.Count("stripe", "currency_mismatch"), Is.EqualTo(1));

        // Amount drift: the event's minor units differ from the quoted checkout.
        var amount = await PostStripe(client, StripePayload(checkoutId, "cs_bind_3", "evt_bind_3", amount: "5000"));
        Assert.That(amount.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await amount.Content.ReadAsStringAsync(), Does.Contain("amount mismatch"));
        Assert.That(recorder.Count("stripe", "amount_mismatch"), Is.EqualTo(1));
    }

    [Test]
    public async Task Ignored_events_persist_their_reason_and_count_ignored()
    {
        using var recorder = new OutcomeRecorder();
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        await PayTest.Put(client, JsonSerializer.Serialize(new
        {
            provider = "chip",
            secret = "chip_sk",
            webhook_secret = pem,
            public_merchant_id = "brand_1"
        }));
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "chip");

        // A verified lifecycle event Pay deliberately does not act on — recorded with its
        // reason so drift shows up as queryable data, not just logs.
        var payload = "{\"event_type\":\"purchase.preauthorized\",\"id\":\"purch_pre\",\"purchase\":{\"id\":\"purch_pre\",\"total\":0,\"currency\":\"MYR\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}}";
        var sig = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/chip/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Signature", sig);
        var response = await client.SendAsync(wh);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("preauthorized"));

        Assert.That(recorder.Count("chip", "ignored"), Is.EqualTo(1));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.PspWebhookEvents.Single(x => x.EventId == "preauth:purch_pre").IgnoreReason,
            Is.EqualTo("preauthorized"));
    }

    [Test]
    public async Task Secret_unavailable_counts_the_503_family()
    {
        using var recorder = new OutcomeRecorder();
        await using var factory = new PayApiFactory { StripeWebhookSecret = "" };
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.GatewayCredentials.Single().WebhookCiphertext = null;
            await db.SaveChangesAsync();
        }

        var (_, checkoutId) = await PayTest.SeedCheckout(client);
        var response = await PostStripe(client, StripePayload(checkoutId, "cs_sec", "evt_sec"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(recorder.Count("stripe", "secret_unavailable"), Is.EqualTo(1));
    }
}
