using System.Net;
using System.Text;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class PayUrlTests
{
    [Test]
    public async Task Checkout_create_and_get_include_pay_url()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var token = createdDoc.RootElement.GetProperty("public_token").GetString();
        var id = createdDoc.RootElement.GetProperty("id").GetString();
        var payUrl = createdDoc.RootElement.GetProperty("pay_url").GetString();
        Assert.That(payUrl, Is.EqualTo("http://pay-checkout.test.example/c/" + token));
        Assert.That(payUrl, Does.Not.Contain("localhost:5179"));

        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/checkouts/" + id);
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var got = await client.SendAsync(get);
        using var gotDoc = JsonDocument.Parse(await got.Content.ReadAsStringAsync());
        Assert.That(gotDoc.RootElement.GetProperty("pay_url").GetString(), Is.EqualTo(payUrl));
    }

    [Test]
    public async Task Payment_link_create_includes_pay_url()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/payment-links")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test","max_payers":1}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("public_token").GetString();
        Assert.That(doc.RootElement.GetProperty("pay_url").GetString(), Is.EqualTo("http://pay-checkout.test.example/c/" + token));
    }

    [Test]
    public async Task Key_mint_includes_pay_url()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Key;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer " + PayTest.MachineKey);
        var created = await client.SendAsync(create);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("pay_url").GetString(), Does.StartWith("http://pay-checkout.test.example/c/"));
    }
}
