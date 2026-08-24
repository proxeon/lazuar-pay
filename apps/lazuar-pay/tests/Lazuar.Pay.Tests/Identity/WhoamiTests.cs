using System.Net;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class WhoamiTests
{
    const string MeJson =
        """{"user_id":"u1","email":"ada@acme.test","name":"Ada Lovelace","is_platform_admin":false,"active_tenant_id":"t1","active_role":"owner","tenants":[{"id":"t1","slug":"acme","name":"Acme","role":"owner","status":"active"}]}""";

    const string EmptyTenantsJson =
        """{"user_id":"u1","email":"ada@acme.test","is_platform_admin":false,"tenants":[]}""";

    [Test]
    public async Task Whoami_maps_org_id_from_one_me()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            Assert.That(req.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(req.RequestUri?.AbsolutePath, Does.EndWith("/me"));
            Assert.That(req.Headers.Authorization?.ToString(), Is.EqualTo("Bearer tok"));
            return FakeOneHandler.Json(HttpStatusCode.OK, MeJson);
        };

        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/whoami");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.That(root.GetProperty("user_id").GetString(), Is.EqualTo("u1"));
        Assert.That(root.GetProperty("email").GetString(), Is.EqualTo("ada@acme.test"));
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo("Ada Lovelace"));
        Assert.That(root.GetProperty("active_org_id").GetString(), Is.EqualTo("t1"));
        Assert.That(root.GetProperty("tenants")[0].GetProperty("id").GetString(), Is.EqualTo("t1"));
        Assert.That(factory.One.SendCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Whoami_allows_empty_tenants()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => FakeOneHandler.Json(HttpStatusCode.OK, EmptyTenantsJson);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/whoami");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("tenants").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task Whoami_without_authorization_is_401_and_skips_one()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/whoami");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Whoami_maps_one_401()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/whoami");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Whoami_maps_one_timeout_to_503()
    {
        await using var factory = new PayApiFactory();
        factory.One.Delay = TimeSpan.FromSeconds(5);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/whoami");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Whoami_maps_one_500_to_503()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/whoami");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }
}
