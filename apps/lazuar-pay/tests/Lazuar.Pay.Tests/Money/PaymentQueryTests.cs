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
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(1));
        Assert.That(doc.RootElement[0].GetProperty("provider").GetString(), Is.EqualTo("test"));
        Assert.That(doc.RootElement[0].GetProperty("status").GetString(), Is.EqualTo("paid"));
        Assert.That(doc.RootElement[0].GetProperty("payer_name").GetString(), Is.EqualTo("Ada"));
        Assert.That(doc.RootElement[0].GetProperty("amount").GetDecimal(), Is.EqualTo(10m));
    }
}
