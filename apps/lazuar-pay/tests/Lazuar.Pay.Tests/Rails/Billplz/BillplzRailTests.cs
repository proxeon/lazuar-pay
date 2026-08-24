using System.Net;
using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Rails.Billplz;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class BillplzRailTests
{
    [Test]
    public async Task Billplz_paid_form_and_localhost_blocked()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());
        Assert.That(factory.Psp.LastUri!.ToString(), Does.Contain("billplz-sandbox"));

        var form = "id=bill_1&paid=true&state=paid&paid_amount=1000&currency=MYR&x_signature=pending&checkout_id=" + checkoutId;
        var fields = BillplzWebhook.ParseForm(form);
        fields["x_signature"] = "pending";
        var mac = BillplzWebhook.ComputeHmac(fields, "xsig", excludeExtra: false);
        form = "id=bill_1&paid=true&state=paid&paid_amount=1000&currency=MYR&x_signature=" + mac + "&checkout_id=" + checkoutId;
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/billplz/t1?checkout_id=" + checkoutId)
        {
            Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        var paid = await client.SendAsync(wh);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/billplz/t1?checkout_id=" + checkoutId)
        {
            Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        var second = await client.SendAsync(replay);
        Assert.That(await second.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Billplz_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"billplz","secret":"bp","webhook_secret":"x","public_merchant_id":"col","environment":"test"}""");
        var (token, _) = await PayTest.SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Billplz_localhost_callback_start_is_400_without_psp_http()
    {
        await using var factory = new PayApiFactory { PublicBaseUrl = "http://localhost:8081" };
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"billplz","secret":"bp","webhook_secret":"x","public_merchant_id":"col","environment":"test"}""");
        var (token, _) = await PayTest.SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("callback base not public"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Billplz_unpaid_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"billplz","secret":"bp","webhook_secret":"xsig","public_merchant_id":"col","environment":"test"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).IsSuccessStatusCode);
        var form = "id=bill_u&paid=false&state=due&paid_amount=0&currency=MYR&x_signature=pending&checkout_id=" + checkoutId;
        var fields = BillplzWebhook.ParseForm(form);
        var mac = BillplzWebhook.ComputeHmac(fields, "xsig", excludeExtra: false);
        form = "id=bill_u&paid=false&state=due&paid_amount=0&currency=MYR&x_signature=" + mac + "&checkout_id=" + checkoutId;
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/billplz/t1?checkout_id=" + checkoutId)
        {
            Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        var response = await client.SendAsync(wh);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("unpaid"));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Billplz_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"billplz","secret":"bp","webhook_secret":"x","public_merchant_id":"col","environment":"test"}""");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/billplz/t1")
        {
            Content = new StringContent("  ", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
