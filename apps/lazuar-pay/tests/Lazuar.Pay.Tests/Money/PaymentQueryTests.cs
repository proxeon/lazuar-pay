using System.Net;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class PaymentQueryTests
{
    [Test]
    public async Task List_payments_includes_provider_and_label()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", System.Text.Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/payments");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(list);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = PayTest.Items(doc.RootElement);
        Assert.That(items.GetArrayLength(), Is.EqualTo(1));
        Assert.That(items[0].GetProperty("provider").GetString(), Is.EqualTo("test"));
        Assert.That(items[0].GetProperty("status").GetString(), Is.EqualTo("paid"));
        Assert.That(items[0].GetProperty("payer_name").GetString(), Is.EqualTo("Ada"));
        Assert.That(items[0].GetProperty("amount").GetDecimal(), Is.EqualTo(10m));
    }

    [Test]
    public async Task List_receipts_includes_number_amount_and_payer()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", System.Text.Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/receipts");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(list);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = PayTest.Items(doc.RootElement);
        Assert.That(items.GetArrayLength(), Is.EqualTo(1));
        Assert.That(items[0].GetProperty("number").GetString(), Does.StartWith("RCPT-"));
        Assert.That(items[0].GetProperty("title").GetString(), Is.EqualTo("Official Receipt"));
        Assert.That(items[0].GetProperty("status").GetString(), Is.EqualTo("issued"));
        Assert.That(items[0].GetProperty("payer_name").GetString(), Is.EqualTo("Ada"));
        Assert.That(items[0].GetProperty("amount").GetDecimal(), Is.EqualTo(10m));
    }

    [Test]
    public async Task Get_receipt_by_id_matches_list_fields()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", System.Text.Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/receipts");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var listed = await client.SendAsync(list);
        using var listDoc = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var id = PayTest.Items(listDoc.RootElement)[0].GetProperty("id").GetString();

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/v1/orgs/t1/receipts/{id}");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(get);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("id").GetString(), Is.EqualTo(id));
        Assert.That(doc.RootElement.GetProperty("number").GetString(), Does.StartWith("RCPT-"));
        Assert.That(doc.RootElement.GetProperty("title").GetString(), Is.EqualTo("Official Receipt"));
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("issued"));
        Assert.That(doc.RootElement.GetProperty("payer_name").GetString(), Is.EqualTo("Ada"));
        Assert.That(doc.RootElement.GetProperty("amount").GetDecimal(), Is.EqualTo(10m));
        Assert.That(doc.RootElement.GetProperty("currency").GetString(), Is.EqualTo("MYR"));
    }

    [Test]
    public async Task Get_receipt_unknown_is_404()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/receipts/missing");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(get);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Get_receipt_other_org_is_403()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Post && path.Contains("/tenants/t2/authz/check"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":false}""");
            }

            return PayTest.Owner(req);
        };
        var client = factory.CreateClient();
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t2/receipts/anything");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(get);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
