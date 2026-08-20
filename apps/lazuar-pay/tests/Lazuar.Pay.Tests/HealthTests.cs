using Microsoft.AspNetCore.Mvc.Testing;

namespace Lazuar.Pay.Tests;

public class HealthTests
{
    [Test]
    public async Task Health_returns_ok()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.That(response.IsSuccessStatusCode);
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ok"));
    }

    [Test]
    public async Task V1_health_returns_ok()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/health");
        Assert.That(response.IsSuccessStatusCode);
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ok"));
    }
}
