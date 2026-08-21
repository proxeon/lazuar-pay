using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lazuar.Pay.Tests;

public class CorsTests
{
    [Test]
    public async Task Health_allows_merchant_origin()
    {
        await using var factory = new WebApplicationFactory<Program>();
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
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:5179");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"), Does.Contain("http://localhost:5179"));
    }

    [Test]
    public async Task Health_does_not_allow_ops_origin()
    {
        await using var factory = new WebApplicationFactory<Program>();
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
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:3004");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }
}
