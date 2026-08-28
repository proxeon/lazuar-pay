using System.Net;
using System.Text;

namespace Lazuar.Pay.Tests;

public class CatalogTests
{
    static HttpResponseMessage Owner(string orgId, HttpRequestMessage req)
    {
        var path = req.RequestUri?.AbsolutePath ?? "";
        if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, $$"""{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"{{orgId}}","role":"owner","status":"active"}]}""");
        }

        if (req.Method == HttpMethod.Post && path.Contains($"/tenants/{orgId}/authz/check"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        }

        return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":false}""");
    }

    [Test]
    public async Task Create_product_as_owner()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Owner("t1", req);
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/products")
        {
            Content = new StringContent("""{"name":"Seat","amount":10}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Member_cannot_create_product()
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
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/products")
        {
            Content = new StringContent("""{"name":"Seat","amount":10}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Payment_link_amount_must_match_catalog_price()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Owner("t1", req);
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}""");
        using var product = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/products")
        {
            Content = new StringContent("""{"name":"Seat","amount":99}""", Encoding.UTF8, "application/json")
        };
        product.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(product);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var doc = System.Text.Json.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var productId = doc.RootElement.GetProperty("id").GetString();
        using var link = new HttpRequestMessage(HttpMethod.Post, "/v1/payment-links")
        {
            Content = new StringContent(
                $$"""{"org_id":"t1","amount":10,"provider":"stripe","product_id":"{{productId}}"}""",
                Encoding.UTF8,
                "application/json")
        };
        link.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(link);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("catalog"));
    }
}
