using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Identity.OneWebhooks;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Tests;

public class OrgWebhookTests
{
    static HttpRequestMessage PutUrl(string orgId, string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, $"/v1/orgs/{orgId}/webhooks")
        {
            Content = new StringContent($$"""{"url":"{{url}}"}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        return req;
    }

    [Test]
    public void Compute_then_verify_round_trip()
    {
        var body = """{"ok":true}""";
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var v1 = OneWebhookSignature.Compute("whsec_abc", body, unix);
        Assert.That(OneWebhookSignature.TryVerify("whsec_abc", body, "v1=" + v1, unix.ToString()), Is.True);
        Assert.That(OneWebhookSignature.TryVerify("whsec_abc", body + "x", "v1=" + v1, unix.ToString()), Is.False);
    }

    [Test]
    public void Production_rejects_loopback_and_metadata()
    {
        var env = new NamedEnv("Production");
        Assert.That(OutboundUrl.TryValidate("http://127.0.0.1/hook", env, out _, out _), Is.False);
        Assert.That(OutboundUrl.TryValidate("http://169.254.169.254/", env, out _, out _), Is.False);
        Assert.That(OutboundUrl.TryValidate("https://app.example/hook", env, out var url, out _), Is.True);
        Assert.That(url, Does.StartWith("https://app.example"));
    }

    [Test]
    public void Testing_allows_loopback()
    {
        Assert.That(OutboundUrl.TryValidate("http://127.0.0.1:9/x", new NamedEnv("Testing"), out _, out _), Is.True);
    }

    [Test]
    public async Task Member_cannot_register()
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
        var response = await client.SendAsync(PutUrl("t1", "http://127.0.0.1:9/hook"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Put_and_get_does_not_echo_secret()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var put = await client.SendAsync(PutUrl("t1", "http://127.0.0.1:9/hook"));
        Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.OK), await put.Content.ReadAsStringAsync());
        var putJson = await put.Content.ReadAsStringAsync();
        using var putDoc = JsonDocument.Parse(putJson);
        var secret = putDoc.RootElement.GetProperty("webhook_secret").GetString();
        Assert.That(secret, Does.StartWith("whsec_"));
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/webhooks");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var got = await client.SendAsync(get);
        var gotJson = await got.Content.ReadAsStringAsync();
        Assert.That(gotJson, Does.Contain("\"webhook_configured\":true"));
        Assert.That(gotJson, Does.Not.Contain(secret!));
    }

    [Test]
    public async Task No_endpoint_still_paid()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Charges.Count(), Is.EqualTo(1));
        Assert.That(db.OrgWebhookDeliveries.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Fulfill_enqueues_and_worker_2xx_verifies()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);
        var client = factory.CreateClient();
        var put = await client.SendAsync(PutUrl("t1", "http://127.0.0.1:9/hook"));
        using var putDoc = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        var secret = putDoc.RootElement.GetProperty("webhook_secret").GetString()!;
        var (token, _) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            Assert.That(db.OrgWebhookDeliveries.Count(), Is.EqualTo(1));
            Assert.That(db.OrgWebhookDeliveries.Single().Status, Is.EqualTo("pending"));
            var dispatch = scope.ServiceProvider.GetRequiredService<OutboundWebhookDispatch>();
            await dispatch.ProcessBatchAsync(CancellationToken.None);
        }

        Assert.That(factory.Psp.SendCount, Is.GreaterThan(0));
        Assert.That(factory.Psp.LastBody, Does.Contain("payment.completed"));
        var sig = factory.Psp.LastRequest!.Headers.GetValues("X-Lazuar-Signature").Single();
        var ts = factory.Psp.LastRequest.Headers.GetValues("X-Lazuar-Timestamp").Single();
        Assert.That(OneWebhookSignature.TryVerify(secret, factory.Psp.LastBody!, sig, ts), Is.True);
        using var after = factory.Services.CreateScope();
        Assert.That(after.ServiceProvider.GetRequiredService<PayDbContext>().OrgWebhookDeliveries.Single().Status, Is.EqualTo("succeeded"));
    }

    [Test]
    public async Task Worker_5xx_retries_401_dead()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await client.SendAsync(PutUrl("t1", "http://127.0.0.1:9/hook"));
        var (token, _) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<OutboundWebhookDispatch>().ProcessBatchAsync(CancellationToken.None);
            var row = scope.ServiceProvider.GetRequiredService<PayDbContext>().OrgWebhookDeliveries.Single();
            Assert.That(row.Status, Is.EqualTo("pending"));
            Assert.That(row.AttemptCount, Is.EqualTo(1));
        }

        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized);
        using var dead = factory.Services.CreateScope();
        var db = dead.ServiceProvider.GetRequiredService<PayDbContext>();
        db.OrgWebhookDeliveries.Single().NextAttemptAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await dead.ServiceProvider.GetRequiredService<OutboundWebhookDispatch>().ProcessBatchAsync(CancellationToken.None);
        Assert.That(db.OrgWebhookDeliveries.Single().Status, Is.EqualTo("dead"));
        Assert.That(db.Charges.Count(), Is.EqualTo(1));
    }

    sealed class NamedEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
