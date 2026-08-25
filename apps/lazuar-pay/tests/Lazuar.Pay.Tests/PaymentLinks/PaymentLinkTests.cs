using System.Net;
using System.Text;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class PaymentLinkTests
{
    static HttpRequestMessage JsonPost(string url, string json) =>
        new(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    [Test]
    public async Task Create_defaults_to_one_payer()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/payment-links", """{"org_id":"t1","amount":10,"provider":"test"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("max_payers").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("unlimited").GetBoolean(), Is.False);
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(doc.RootElement.GetProperty("remaining").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("public_token").GetString(), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Create_unlimited_has_null_max()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, unlimited: true);
        var get = await client.GetAsync($"/v1/pay/{token}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(doc.RootElement.GetProperty("max_payers").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(doc.RootElement.GetProperty("remaining").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Create_max_zero_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/payment-links", """{"org_id":"t1","amount":10,"provider":"test","max_payers":0}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("max_payers"));
    }

    [Test]
    public async Task Create_without_bearer_is_401()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.SendAsync(JsonPost("/v1/payment-links", """{"org_id":"t1","amount":10,"provider":"test"}"""));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task List_returns_newest_first_with_capacity()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.SeedPaymentLink(client, maxPayers: 1);
        await PayTest.SeedPaymentLink(client, maxPayers: 3);

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/payment-links");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(list);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(2));
        Assert.That(doc.RootElement[0].GetProperty("max_payers").GetInt32(), Is.EqualTo(3));
        Assert.That(doc.RootElement[0].GetProperty("remaining").GetInt32(), Is.EqualTo(3));
        Assert.That(doc.RootElement[1].GetProperty("max_payers").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task List_other_org_is_403()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","email":"ada@acme.test","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"}]}""");
            }

            if (req.Method == HttpMethod.Post && path.Contains("/tenants/t1/authz/check"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
            }

            if (req.Method == HttpMethod.Post && path.Contains("/authz/check"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":false}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };
        var client = factory.CreateClient();
        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t2/payment-links");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(list);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Two_people_can_pay_a_link_of_two()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, maxPayers: 2);

        var a = await PayTest.StartPay(client, token, "slot-aaa-1");
        Assert.That(a.StatusCode, Is.EqualTo(HttpStatusCode.OK), await a.Content.ReadAsStringAsync());
        var b = await PayTest.StartPay(client, token, "slot-bbb-2");
        Assert.That(b.StatusCode, Is.EqualTo(HttpStatusCode.OK), await b.Content.ReadAsStringAsync());
        var c = await PayTest.StartPay(client, token, "slot-ccc-3");
        Assert.That(c.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), await c.Content.ReadAsStringAsync());
        Assert.That(await c.Content.ReadAsStringAsync(), Does.Contain("full"));

        var paidA = await client.GetAsync($"/v1/pay/{token}?slot_key=slot-aaa-1");
        using var paidDoc = JsonDocument.Parse(await paidA.Content.ReadAsStringAsync());
        Assert.That(paidDoc.RootElement.GetProperty("status").GetString(), Is.EqualTo("paid"));

        var other = await client.GetAsync($"/v1/pay/{token}?slot_key=slot-ccc-3");
        using var otherDoc = JsonDocument.Parse(await other.Content.ReadAsStringAsync());
        Assert.That(otherDoc.RootElement.GetProperty("status").GetString(), Is.EqualTo("full"));
        Assert.That(otherDoc.RootElement.GetProperty("remaining").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task Same_slot_start_twice_does_not_take_two_seats()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"chip","secret":"chip_sk","webhook_secret":"k","public_merchant_id":"brand_1"}""");
        var (token, _) = await PayTest.SeedPaymentLink(client, "chip", maxPayers: 2);

        var first = await PayTest.StartPay(client, token, "slot-same-1", """{"name":"Ada","email":"ada@acme.test"}""");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK), await first.Content.ReadAsStringAsync());
        var second = await PayTest.StartPay(client, token, "slot-same-1", """{"name":"Ada","email":"ada@acme.test"}""");
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK), await second.Content.ReadAsStringAsync());
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/payment-links");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var listed = await client.SendAsync(list);
        using var doc = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement[0].GetProperty("taken_count").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement[0].GetProperty("remaining").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task Unlimited_accepts_three_payers()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, unlimited: true);
        Assert.That((await PayTest.StartPay(client, token, "slot-unl-01")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PayTest.StartPay(client, token, "slot-unl-02")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PayTest.StartPay(client, token, "slot-unl-03")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var get = await client.GetAsync($"/v1/pay/{token}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(doc.RootElement.GetProperty("paid_count").GetInt32(), Is.EqualTo(3));
        Assert.That(doc.RootElement.GetProperty("remaining").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task One_person_link_shows_paid_without_slot_after_pay()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, maxPayers: 1);
        Assert.That((await PayTest.StartPay(client, token, "slot-only-1")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var get = await client.GetAsync($"/v1/pay/{token}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("paid"));
    }

    [Test]
    public async Task Start_link_without_slot_key_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client);
        using var start = JsonPost($"/v1/pay/{token}/start", """{"name":"Ada"}""");
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("slot_key"));
    }

    [Test]
    public async Task Public_get_does_not_need_bearer()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client);
        var get = await client.GetAsync($"/v1/pay/{token}");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(factory.One.SendCount, Is.GreaterThan(0));
        var after = factory.One.SendCount;
        var again = await client.GetAsync($"/v1/pay/{token}");
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(factory.One.SendCount, Is.EqualTo(after));
    }
}
