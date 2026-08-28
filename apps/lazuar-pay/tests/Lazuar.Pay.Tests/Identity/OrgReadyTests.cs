using System.Net;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class OrgReadyTests
{
    [Test]
    public async Task Ready_when_one_allows_member()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            Assert.That(req.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(req.RequestUri?.AbsolutePath, Does.Contain("/tenants/t1/authz/check"));
            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };

        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("org_id").GetString(), Is.EqualTo("t1"));
        Assert.That(doc.RootElement.GetProperty("ready").GetBoolean(), Is.True);
        Assert.That(factory.One.LastBody, Does.Contain("\"relation\":\"member\""));
        Assert.That(factory.One.LastBody, Does.Contain("\"type\":\"tenant\""));
        Assert.That(factory.One.LastBody, Does.Contain("\"id\":\"t1\""));
        Assert.That(factory.One.LastBody, Does.Not.Contain("user_id"));
    }

    [Test]
    public async Task Ready_forbidden_when_allowed_false()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":false}""");
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Ready_forbidden_when_one_403()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => new HttpResponseMessage(HttpStatusCode.Forbidden);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Ready_503_when_one_500()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Ready_400_when_one_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => FakeOneHandler.Json(
            HttpStatusCode.BadRequest,
            """{"detail":"The value 't1' is not valid."}""");
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("The value 't1' is not valid."));
    }

    [Test]
    public async Task Ready_429_when_one_429()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => new HttpResponseMessage((HttpStatusCode)429);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode)429));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Identity provider rate limited"));
    }

    [Test]
    public async Task Ready_403_passes_through_suspended_detail()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => FakeOneHandler.Json(
            HttpStatusCode.Forbidden,
            """{"detail":"Tenant is suspended."}""");
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Tenant is suspended."));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("Not a member"));
    }

    [Test]
    public async Task Ready_false_when_charges_paused()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.OrgSettings.Add(new OrgSettingsRow { OrgId = "t1", ChargesPaused = true });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("ready").GetBoolean(), Is.False);
    }

    [Test]
    public void Ready_is_false_without_vault_when_test_is_off()
    {
        Assert.That(OrgReadyEndpoints.IsReady(chargesPaused: false, hasVault: false, allowsTest: false), Is.False);
        Assert.That(OrgReadyEndpoints.IsReady(chargesPaused: false, hasVault: true, allowsTest: false), Is.True);
        Assert.That(OrgReadyEndpoints.IsReady(chargesPaused: true, hasVault: true, allowsTest: true), Is.False);
    }

    [Test]
    public async Task Ready_401_without_bearer_skips_one()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/orgs/t1/ready");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Ready_checks_path_org_not_header()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            Assert.That(req.RequestUri?.AbsolutePath, Does.Contain("/tenants/path-org/authz/check"));
            Assert.That(factory.One.LastBody, Does.Contain("\"id\":\"path-org\""));
            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };

        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/path-org/ready");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        request.Headers.TryAddWithoutValidation("X-Lazuar-Tenant-Id", "header-org");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("org_id").GetString(), Is.EqualTo("path-org"));
    }
}
