using System.Net;
using System.Text;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Regression tests for per-rail currency validation (issues 003 and 014 in issues/001):
/// zero-decimal currencies must be rejected (ToMinor assumes ×100 — a JPY quote would be
/// charged 100× at the processor), and rails only settle what they bill (Billplz/CHIP are
/// MYR-only, Razorpay settles INR) — a USD quote on those used to collect ringgit while
/// the ledger booked dollars.
/// </summary>
public class CurrencyValidationTests
{
    static async Task<HttpResponseMessage> Create(
        HttpClient client, string path, string json)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        return await client.SendAsync(req);
    }

    [Test]
    public async Task Zero_decimal_currency_is_rejected_on_checkout_and_link()
    {
        // Issue 003: JPY is zero-decimal; ToMinor(x) = x*100 would charge 100× the quote.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");

        var checkout = await Create(client, "/v1/checkouts",
            """{"org_id":"t1","amount":1000,"currency":"JPY","provider":"stripe"}""");
        Assert.That(checkout.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await checkout.Content.ReadAsStringAsync());
        Assert.That(await checkout.Content.ReadAsStringAsync(), Does.Contain("not supported"));

        var link = await Create(client, "/v1/payment-links",
            """{"org_id":"t1","amount":1000,"currency":"JPY","provider":"stripe","max_payers":1}""");
        Assert.That(link.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await link.Content.ReadAsStringAsync());

        // Sanity: a supported two-decimal currency on the same rail is still accepted.
        var ok = await Create(client, "/v1/checkouts",
            """{"org_id":"t1","amount":10,"currency":"USD","provider":"stripe"}""");
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.Created), await ok.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Billplz_and_chip_reject_non_myr_and_razorpay_rejects_non_inr()
    {
        // Issue 014: Billplz bills MYR only — the bill payload carries no currency at all —
        // so a USD quote collected ringgit while the books recorded USD.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"billplz","secret":"plz_key","webhook_secret":"wb_key","public_merchant_id":"bar_1","environment":"test"}""");
        await PayTest.PutChip(client);
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"rzp_wh"}""");

        var billplzUsd = await Create(client, "/v1/payment-links",
            """{"org_id":"t1","amount":10,"currency":"USD","provider":"billplz","max_payers":1}""");
        Assert.That(billplzUsd.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await billplzUsd.Content.ReadAsStringAsync());

        var chipUsd = await Create(client, "/v1/checkouts",
            """{"org_id":"t1","amount":10,"currency":"USD","provider":"chip"}""");
        Assert.That(chipUsd.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await chipUsd.Content.ReadAsStringAsync());

        var razorMyr = await Create(client, "/v1/checkouts",
            """{"org_id":"t1","amount":10,"currency":"MYR","provider":"razorpay"}""");
        Assert.That(razorMyr.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await razorMyr.Content.ReadAsStringAsync());

        // The rails' own currencies still work, including the MYR default.
        var billplzMyr = await Create(client, "/v1/payment-links",
            """{"org_id":"t1","amount":10,"currency":"MYR","provider":"billplz","max_payers":1}""");
        Assert.That(billplzMyr.StatusCode, Is.EqualTo(HttpStatusCode.Created), await billplzMyr.Content.ReadAsStringAsync());

        var razorInr = await Create(client, "/v1/checkouts",
            """{"org_id":"t1","amount":10,"currency":"INR","provider":"razorpay"}""");
        Assert.That(razorInr.StatusCode, Is.EqualTo(HttpStatusCode.Created), await razorInr.Content.ReadAsStringAsync());
    }
}
