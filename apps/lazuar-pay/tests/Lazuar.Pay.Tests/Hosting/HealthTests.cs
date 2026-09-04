using Lazuar.Pay.Hosting;
using Microsoft.AspNetCore.Http;

namespace Lazuar.Pay.Tests;

public class HealthTests
{
    [Test]
    public async Task Health_returns_ok()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.That(response.IsSuccessStatusCode);
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ok"));
    }

    [Test]
    public async Task V1_health_returns_ok()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/health");
        Assert.That(response.IsSuccessStatusCode);
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ok"));
    }

    [Test]
    public async Task Health_does_not_call_one()
    {
        await using var factory = new PayApiFactory();
        factory.One.ThrowOnSend = true;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        var v1 = await client.GetAsync("/v1/health");
        Assert.That(response.IsSuccessStatusCode);
        Assert.That(v1.IsSuccessStatusCode);
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Unversioned_ready_returns_200_when_database_is_up()
    {
        await using var factory = new PayApiFactory();
        factory.One.ThrowOnSend = true;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/ready");
        Assert.That(response.IsSuccessStatusCode);
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ready"));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public void Ready_false_when_cannot_connect()
    {
        var result = PayReady.From(false);
        var status = ((IStatusCodeHttpResult)result).StatusCode;
        Assert.That(status, Is.EqualTo(503));
    }
}
