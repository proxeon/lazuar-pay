using System.Net;
using Lazuar.Pay.Hosting;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Tests;

public class CorsTests
{
    [Test]
    public async Task Health_allows_merchant_origin()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:5178");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Does.Contain("http://localhost:5178"));
    }

    [Test]
    public async Task Health_allows_checkout_origin()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:5179");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Does.Contain("http://localhost:5179"));
    }

    [Test]
    public async Task Health_allows_preview_checkout_origin()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:4179");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Does.Contain("http://localhost:4179"));
    }

    [Test]
    public async Task Health_does_not_allow_ops_origin()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:3003");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }

    [Test]
    public async Task Health_does_not_allow_portal_origin()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:3004");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }

    [Test]
    public async Task Health_allows_configured_extra_origin()
    {
        await using var factory = new PayApiFactory { CorsOrigins = "https://checkout.example" };
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "https://checkout.example");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Does.Contain("https://checkout.example"));
    }

    [Test]
    public async Task Configured_origins_replace_laptop_list()
    {
        await using var factory = new PayApiFactory { CorsOrigins = "https://checkout.example" };
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:5179");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }

    [Test]
    public async Task Public_pay_get_allows_checkout_origin()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/pay/missing");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:5179");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Does.Contain("http://localhost:5179"));
    }

    [Test]
    public void Empty_cors_in_production_fails_boot()
    {
        Assert.That(
            () => PayCors.Resolve(null, Environments.Production),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("Pay:CorsOrigins"));
        Assert.That(
            () => PayCors.Resolve("  ", Environments.Staging),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("Pay:CorsOrigins"));
    }

    [Test]
    public void Empty_cors_in_development_uses_laptop_list()
    {
        Assert.That(PayCors.Resolve(null, Environments.Development), Is.EqualTo(PayCors.DevelopmentOrigins));
        Assert.That(PayCors.Resolve("", "Testing"), Is.EqualTo(PayCors.DevelopmentOrigins));
    }
}
