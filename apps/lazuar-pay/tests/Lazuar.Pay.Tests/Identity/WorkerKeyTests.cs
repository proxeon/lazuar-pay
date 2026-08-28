using System.Net;
using Lazuar.Pay.Identity.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Lazuar.Pay.Tests;

public class WorkerKeyTests
{
    [Test]
    public void Rejects_sk_live_in_worker_slot()
    {
        Assert.Throws<InvalidOperationException>(() => OneWorkerClient.ThrowIfInvalidKey("sk_live_xxx"));
        Assert.Throws<InvalidOperationException>(() => OneWorkerClient.ThrowIfInvalidKey("sk_test_xxx"));
        OneWorkerClient.ThrowIfInvalidKey("lzr_sk_tenantbound");
    }

    [Test]
    public void Requires_worker_org_when_key_set()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["One:ApiKey"] = "lzr_sk_job" })
            .Build();
        Assert.Throws<InvalidOperationException>(() => OneWorkerClient.ThrowIfInvalid(config));
        var ok = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["One:ApiKey"] = "lzr_sk_job",
                ["One:WorkerOrgId"] = "t1"
            })
            .Build();
        OneWorkerClient.ThrowIfInvalid(ok);
    }

    [Test]
    public void Worker_client_does_not_share_headers_with_interactive()
    {
        using var workerHttp = new HttpClient { BaseAddress = new Uri("http://one.test/api/v1/") };
        using var interactiveHttp = new HttpClient { BaseAddress = new Uri("http://one.test/api/v1/") };
        var options = Options.Create(new OneOptions
        {
            BaseUrl = "http://one.test/api/v1",
            ApiKey = "lzr_sk_job",
            WorkerOrgId = "t1"
        });
        var worker = new OneWorkerClient(workerHttp, options);
        var interactive = new OneClient(interactiveHttp, Options.Create(new OneOptions { BaseUrl = "http://one.test/api/v1" }));
        Assert.That(worker.Http.DefaultRequestHeaders.Authorization?.ToString(), Does.Contain("lzr_sk_job"));
        Assert.That(interactive.Http.DefaultRequestHeaders.Authorization, Is.Null);
        Assert.That(worker.WorkerOrgId, Is.EqualTo("t1"));
    }

    [Test]
    public async Task Missing_request_bearer_still_401_when_env_key_set()
    {
        await using var factory = new PayApiFactory { OneApiKey = "lzr_sk_job", OneWorkerOrgId = "t1" };
        factory.One.Responder = PayTest.Key;
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test"}""", System.Text.Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }
}
