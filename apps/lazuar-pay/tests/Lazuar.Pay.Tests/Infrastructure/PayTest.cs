using System.Net;
using System.Text;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

internal static class PayTest
{
    public static HttpResponseMessage Owner(HttpRequestMessage req)
    {
        var path = req.RequestUri?.AbsolutePath ?? "";
        if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"}]}""");
        }

        return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
    }

    public static async Task Put(HttpClient client, string json)
    {
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(keys);
        Assert.That(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    public static async Task<(string Token, string CheckoutId)> SeedCheckout(HttpClient client, string provider = "stripe")
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent(
                $$"""{"org_id":"t1","amount":10,"provider":"{{provider}}"}""",
                Encoding.UTF8,
                "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return (doc.RootElement.GetProperty("public_token").GetString()!, doc.RootElement.GetProperty("id").GetString()!);
    }

    public static async Task<(string Token, string LinkId)> SeedPaymentLink(
        HttpClient client,
        string provider = "test",
        int? maxPayers = 1,
        bool unlimited = false)
    {
        var maxJson = unlimited ? "null" : maxPayers.ToString();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/payment-links")
        {
            Content = new StringContent(
                $$"""{"org_id":"t1","amount":10,"provider":"{{provider}}","max_payers":{{maxJson}},"unlimited":{{(unlimited ? "true" : "false")}}}""",
                Encoding.UTF8,
                "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return (doc.RootElement.GetProperty("public_token").GetString()!, doc.RootElement.GetProperty("id").GetString()!);
    }

    public static async Task<HttpResponseMessage> StartPay(HttpClient client, string token, string? slotKey, string json = """{"name":"Ada"}""")
    {
        using var doc = JsonDocument.Parse(json);
        var payload = new Dictionary<string, JsonElement>();
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            payload[p.Name] = p.Value.Clone();
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in payload)
            {
                writer.WritePropertyName(key);
                value.WriteTo(writer);
            }

            if (slotKey is not null)
            {
                writer.WriteString("slot_key", slotKey);
            }

            writer.WriteEndObject();
        }

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent(Encoding.UTF8.GetString(buffer.ToArray()), Encoding.UTF8, "application/json")
        };
        return await client.SendAsync(start);
    }
}
