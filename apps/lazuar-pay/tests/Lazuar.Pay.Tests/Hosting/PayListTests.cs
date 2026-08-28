using System.Net;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class PayListTests
{
    [Test]
    public async Task Checkout_list_pages_with_cursor_on_v1()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.SeedCheckout(client, "test");
        await PayTest.SeedCheckout(client, "test");
        await PayTest.SeedCheckout(client, "test");

        using var first = Get("/v1/orgs/t1/checkouts?limit=2");
        var page1 = await client.SendAsync(first);
        Assert.That(page1.StatusCode, Is.EqualTo(HttpStatusCode.OK), await page1.Content.ReadAsStringAsync());
        using var doc1 = JsonDocument.Parse(await page1.Content.ReadAsStringAsync());
        Assert.That(PayTest.Items(doc1.RootElement).GetArrayLength(), Is.EqualTo(2));
        var cursor = doc1.RootElement.GetProperty("next_cursor").GetString();
        Assert.That(cursor, Is.Not.Null.And.Not.Empty);

        using var second = Get("/v1/orgs/t1/checkouts?limit=2&after=" + cursor);
        var page2 = await client.SendAsync(second);
        using var doc2 = JsonDocument.Parse(await page2.Content.ReadAsStringAsync());
        Assert.That(PayTest.Items(doc2.RootElement).GetArrayLength(), Is.EqualTo(1));
        Assert.That(doc2.RootElement.GetProperty("next_cursor").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    static HttpRequestMessage Get(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        return req;
    }
}
