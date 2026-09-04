using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task Create_stores_label_for_solana_without_product()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var address = SolanaVaultTests.SampleAddress();
        await PayTest.Put(client, JsonSerializer.Serialize(new
        {
            provider = "solana",
            public_merchant_id = address,
            environment = "devnet"
        }));
        using var create = JsonPost("/v1/payment-links",
            """{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC","label":"Membership Sep"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(created.RootElement.GetProperty("label").GetString(), Is.EqualTo("Membership Sep"));

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/payment-links");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var listed = await client.SendAsync(list);
        using var doc = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        Assert.That(PayTest.Items(doc.RootElement)[0].GetProperty("label").GetString(), Is.EqualTo("Membership Sep"));
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
        var items = PayTest.Items(doc.RootElement);
        Assert.That(items.GetArrayLength(), Is.EqualTo(2));
        Assert.That(items[0].GetProperty("max_payers").GetInt32(), Is.EqualTo(3));
        Assert.That(items[0].GetProperty("remaining").GetInt32(), Is.EqualTo(3));
        Assert.That(items[1].GetProperty("max_payers").GetInt32(), Is.EqualTo(1));
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
        await PayTest.PutChip(client);
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
        var items = PayTest.Items(doc.RootElement);
        Assert.That(items[0].GetProperty("taken_count").GetInt32(), Is.EqualTo(1));
        Assert.That(items[0].GetProperty("remaining").GetInt32(), Is.EqualTo(1));
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
    public async Task One_person_link_shows_already_paid_without_slot_after_pay()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, maxPayers: 1);
        Assert.That((await PayTest.StartPay(client, token, "slot-only-1")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var get = await client.GetAsync($"/v1/pay/{token}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("already_paid"));
        Assert.That(doc.RootElement.GetProperty("mine").GetBoolean(), Is.False);
        Assert.That(doc.RootElement.GetProperty("started").GetBoolean(), Is.False);
        Assert.That(doc.RootElement.TryGetProperty("payer_email", out _), Is.False);
    }

    [Test]
    public async Task One_person_link_shows_paid_with_payer_slot_after_pay()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, maxPayers: 1);
        Assert.That((await PayTest.StartPay(client, token, "slot-only-1")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var get = await client.GetAsync($"/v1/pay/{token}?slot_key=slot-only-1");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("paid"));
        Assert.That(doc.RootElement.GetProperty("mine").GetBoolean(), Is.True);
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

    [Test]
    public async Task Member_cannot_create_payment_link()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"member","status":"active"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/payment-links", """{"org_id":"t1","amount":10,"provider":"test"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_can_create_payment_link()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"admin","status":"active"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/payment-links", """{"org_id":"t1","amount":10,"provider":"test"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Suspended_writer_cannot_create_payment_link()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"suspended"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/payment-links", """{"org_id":"t1","amount":10,"provider":"test"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("suspended"));
    }

    [Test]
    public async Task Child_public_token_loads_parent_occupancy()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        var (token, linkId) = await PayTest.SeedPaymentLink(client, "chip", maxPayers: 2);
        Assert.That((await PayTest.StartPay(client, token, "slot-alias-1", """{"name":"Ada","email":"ada@acme.test"}""")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = factory.Services.CreateScope();
        var child = scope.ServiceProvider.GetRequiredService<PayDbContext>().Checkouts.Single(x => x.PaymentLinkId == linkId);
        var get = await client.GetAsync($"/v1/pay/{child.PublicToken}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("remaining").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("max_payers").GetInt32(), Is.EqualTo(2));
        Assert.That(doc.RootElement.GetProperty("taken_count").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task Pause_expires_open_reservations()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        var (token, _) = await PayTest.SeedPaymentLink(client, "chip", maxPayers: 1);
        Assert.That((await PayTest.StartPay(client, token, "slot-pause-1", """{"name":"Ada","email":"ada@acme.test"}""")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused = true;
            await db.SaveChangesAsync();
        }

        var get = await client.GetAsync($"/v1/pay/{token}?slot_key=slot-other-2");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(doc.RootElement.GetProperty("remaining").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("taken_count").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task Two_chip_starts_hold_open_seats_on_a_link_of_two()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        var (token, _) = await PayTest.SeedPaymentLink(client, "chip", maxPayers: 2);
        Assert.That((await PayTest.StartPay(client, token, "slot-open-a", """{"name":"Ada","email":"ada@acme.test"}""")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PayTest.StartPay(client, token, "slot-open-b", """{"name":"Bob","email":"bob@acme.test"}""")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PayTest.StartPay(client, token, "slot-open-c", """{"name":"Cid","email":"cid@acme.test"}""")).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var get = await client.GetAsync($"/v1/pay/{token}?slot_key=slot-open-c");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("full"));
        Assert.That(doc.RootElement.GetProperty("paid_count").GetInt32(), Is.EqualTo(0));
        Assert.That(doc.RootElement.GetProperty("taken_count").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task Start_rate_limit_is_429()
    {
        await using var factory = new PayApiFactory { StartMaxPerMinute = 2 };
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, unlimited: true);
        Assert.That((await PayTest.StartPay(client, token, "slot-lim-01")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PayTest.StartPay(client, token, "slot-lim-02")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var third = await PayTest.StartPay(client, token, "slot-lim-03");
        Assert.That(third.StatusCode, Is.EqualTo((HttpStatusCode)429));
    }

    [Test]
    public async Task Concurrent_start_on_one_person_link_admits_one_psp()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) =>
        {
            Thread.Sleep(120);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"purch_race","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
            };
        };
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        await PayTest.PutChip(clientA);
        var (token, _) = await PayTest.SeedPaymentLink(clientA, "chip", maxPayers: 1);

        var email = """{"name":"Ada","email":"ada@acme.test"}""";
        var first = PayTest.StartPay(clientA, token, "slot-race-a1", email);
        var second = PayTest.StartPay(clientB, token, "slot-race-b2", email);
        await Task.WhenAll(first, second);

        var codes = new[] { first.Result.StatusCode, second.Result.StatusCode };
        Assert.That(codes, Does.Contain(HttpStatusCode.OK));
        Assert.That(codes, Does.Contain(HttpStatusCode.Conflict));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(await db.Documents.CountAsync(), Is.EqualTo(0));
        Assert.That(await db.Checkouts.CountAsync(x => x.Status == "open"), Is.EqualTo(1));
    }

    [Test]
    public async Task Concurrent_test_start_on_one_person_link_mints_one_receipt()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(clientA, maxPayers: 1);

        var first = PayTest.StartPay(clientA, token, "slot-t-race-a");
        var second = PayTest.StartPay(clientB, token, "slot-t-race-b");
        await Task.WhenAll(first, second);

        var codes = new[] { first.Result.StatusCode, second.Result.StatusCode };
        Assert.That(codes, Does.Contain(HttpStatusCode.OK));
        Assert.That(codes, Does.Contain(HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(await db.Documents.CountAsync(x => x.Title == "Official Receipt"), Is.EqualTo(1));
        Assert.That(await db.Checkouts.CountAsync(x => x.Status == "paid"), Is.EqualTo(1));
    }

    [Test]
    public async Task Chip_start_without_email_does_not_occupy_the_only_seat()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        var (token, _) = await PayTest.SeedPaymentLink(client, "chip", maxPayers: 1);

        var missing = await PayTest.StartPay(client, token, "slot-ghost-1");
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await missing.Content.ReadAsStringAsync());
        Assert.That(await missing.Content.ReadAsStringAsync(), Does.Contain("email is required"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));

        var other = await client.GetAsync($"/v1/pay/{token}?slot_key=slot-other-2");
        using var openDoc = JsonDocument.Parse(await other.Content.ReadAsStringAsync());
        Assert.That(openDoc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(openDoc.RootElement.GetProperty("remaining").GetInt32(), Is.EqualTo(1));

        var ok = await PayTest.StartPay(client, token, "slot-other-2", """{"name":"Ada","email":"ada@acme.test"}""");
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK), await ok.Content.ReadAsStringAsync());
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Billplz_localhost_callback_400_frees_the_seat()
    {
        await using var factory = new PayApiFactory { PublicBaseUrl = "http://localhost:8081" };
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}""");
        var (token, _) = await PayTest.SeedPaymentLink(client, "billplz", maxPayers: 1);

        var first = await PayTest.StartPay(client, token, "slot-bp-fail", """{"name":"Ada","email":"ada@acme.test"}""");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await first.Content.ReadAsStringAsync());
        Assert.That(await first.Content.ReadAsStringAsync(), Does.Contain("callback base"));

        var other = await client.GetAsync($"/v1/pay/{token}?slot_key=slot-bp-next");
        using var doc = JsonDocument.Parse(await other.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(doc.RootElement.GetProperty("remaining").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("taken_count").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task Abandoned_open_reservation_expires_and_second_slot_can_start()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_old","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        var (token, linkId) = await PayTest.SeedPaymentLink(client, "chip", maxPayers: 1);

        var start = await PayTest.StartPay(client, token, "slot-stale-1", """{"name":"Ada","email":"ada@acme.test"}""");
        Assert.That(start.StatusCode, Is.EqualTo(HttpStatusCode.OK), await start.Content.ReadAsStringAsync());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            var child = await db.Checkouts.FirstAsync(x => x.PaymentLinkId == linkId && x.SlotKey == "slot-stale-1");
            child.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-31);
            await db.SaveChangesAsync();
        }

        var staleGet = await client.GetAsync($"/v1/pay/{token}?slot_key=slot-stale-1");
        using var staleDoc = JsonDocument.Parse(await staleGet.Content.ReadAsStringAsync());
        Assert.That(staleDoc.RootElement.GetProperty("status").GetString(), Is.EqualTo("expired"));

        var next = await PayTest.StartPay(client, token, "slot-fresh-2", """{"name":"Bob","email":"bob@acme.test"}""");
        Assert.That(next.StatusCode, Is.EqualTo(HttpStatusCode.OK), await next.Content.ReadAsStringAsync());

        using var after = factory.Services.CreateScope();
        var payDb = after.ServiceProvider.GetRequiredService<PayDbContext>();
        var fulfill = after.ServiceProvider.GetRequiredService<IFulfillPaid>();
        var expiredId = await payDb.Checkouts
            .Where(x => x.SlotKey == "slot-stale-1")
            .Select(x => x.Id)
            .FirstAsync();
        await fulfill.FulfillPaidAsync(expiredId, "chip", "purch_old", CancellationToken.None);
        Assert.That(await payDb.Documents.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task Second_fulfill_on_max_one_link_does_not_mint_a_second_receipt()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, linkId) = await PayTest.SeedPaymentLink(client, maxPayers: 1);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        var first = OpenChild(linkId, "slot-over-a");
        var extra = OpenChild(linkId, "slot-over-b");
        db.Checkouts.AddRange(first, extra);
        await db.SaveChangesAsync();

        var fulfill = scope.ServiceProvider.GetRequiredService<IFulfillPaid>();
        await fulfill.FulfillPaidAsync(first.Id, "test", "ref-a", CancellationToken.None);
        await fulfill.FulfillPaidAsync(extra.Id, "test", "ref-b", CancellationToken.None);

        Assert.That(await db.Documents.CountAsync(x => x.Title == "Official Receipt"), Is.EqualTo(1));
        Assert.That(await db.Charges.CountAsync(), Is.EqualTo(1));
        await db.Entry(extra).ReloadAsync();
        Assert.That(extra.Status, Is.EqualTo("expired"));
    }

    [Test]
    public async Task List_over_admit_is_over_capacity_not_silent_full()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, linkId) = await PayTest.SeedPaymentLink(client, maxPayers: 1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            var first = OpenChild(linkId, "slot-over-a");
            first.Status = "paid";
            var extra = OpenChild(linkId, "slot-over-b");
            extra.Status = "paid";
            db.Checkouts.AddRange(first, extra);
            await db.SaveChangesAsync();
        }

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/payment-links");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var listed = await client.SendAsync(list);
        Assert.That(listed.StatusCode, Is.EqualTo(HttpStatusCode.OK), await listed.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var items = PayTest.Items(doc.RootElement);
        Assert.That(items[0].GetProperty("taken_count").GetInt32(), Is.EqualTo(2));
        Assert.That(items[0].GetProperty("max_payers").GetInt32(), Is.EqualTo(1));
        Assert.That(items[0].GetProperty("remaining").GetInt32(), Is.EqualTo(-1));
        Assert.That(items[0].GetProperty("status").GetString(), Is.EqualTo("over_capacity"));
    }

    static CheckoutRow OpenChild(string linkId, string slot) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            OrgId = "t1",
            Provider = "test",
            PaymentLinkId = linkId,
            SlotKey = slot,
            PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray()),
            Amount = 10m,
            Currency = "MYR",
            Status = "open",
            Interval = "one_off",
            CreatedAt = DateTimeOffset.UtcNow
        };
}
