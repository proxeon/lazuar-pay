using System.Net;
using System.Text;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class PublicPayTests
{
    static HttpResponseMessage Owner(HttpRequestMessage req)
    {
        var path = req.RequestUri?.AbsolutePath ?? "";
        if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"}]}""");
        }

        return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
    }

    [Test]
    public async Task Public_get_does_not_need_bearer()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("public_token").GetString();
        var get = await client.GetAsync($"/v1/pay/{token}");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(factory.One.SendCount, Is.GreaterThan(0));
        var after = factory.One.SendCount;
        var again = await client.GetAsync($"/v1/pay/{token}");
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(factory.One.SendCount, Is.EqualTo(after));
    }

    [Test]
    public async Task Public_missing_is_404()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/pay/missing");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Empty_webhook_is_400()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent("", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
