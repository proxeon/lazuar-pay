using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Messaging.Infrastructure.Configuration;
using Modules.Messaging.Infrastructure.Email;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Messaging;

[TestFixture]
public class ResendEmailServiceTests
{
    private static ResendEmailService CreateSut(
        HttpMessageHandler handler,
        string platformApiKey = "platform_key",
        string platformSender = "platform@lazuar.test")
    {
        var factory = new FixedHttpClientFactory(handler);
        var options = Options.Create(new ResendOptions
        {
            ApiKey = platformApiKey,
            SenderEmail = platformSender
        });
        return new ResendEmailService(factory, options, NullLogger<ResendEmailService>.Instance);
    }

    [Test]
    public async Task SendEmailAsync_TenantByok_PostsOrgTagAndReturnsProviderId()
    {
        var orgId = Guid.CreateVersion7();
        string? capturedBody = null;
        string? capturedAuth = null;

        var handler = new CaptureHandler(async (request, ct) =>
        {
            capturedAuth = request.Headers.Authorization?.ToString();
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"re_test_123"}""", Encoding.UTF8, "application/json")
            };
        });

        var sut = CreateSut(handler);
        var providerId = await sut.SendEmailAsync(
            "user@example.com",
            "Subject",
            "<p>Body</p>",
            orgId,
            tenantApiKey: "tenant_key",
            tenantSenderEmail: "from@tenant.test");

        providerId.Should().Be("re_test_123");
        capturedAuth.Should().Be("Bearer tenant_key");
        capturedBody.Should().NotBeNull();

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        root.GetProperty("from").GetString().Should().Be("from@tenant.test");
        root.GetProperty("to")[0].GetString().Should().Be("user@example.com");
        root.GetProperty("subject").GetString().Should().Be("Subject");
        root.GetProperty("html").GetString().Should().Be("<p>Body</p>");

        var tags = root.GetProperty("tags");
        tags.GetArrayLength().Should().Be(1);
        tags[0].GetProperty("name").GetString().Should().Be(ResendEmailService.OrgTagName);
        tags[0].GetProperty("name").GetString().Should().Be("org");
        tags[0].GetProperty("value").GetString().Should().Be(orgId.ToString());
    }

    [Test]
    public async Task SendEmailAsync_WithUnsubscribe_AddsListUnsubscribeHeaders()
    {
        string? capturedBody = null;
        var handler = new CaptureHandler(async (request, ct) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"re_1"}""", Encoding.UTF8, "application/json")
            };
        });

        var sut = CreateSut(handler);
        var unsub = "https://example.com/unsub";
        await sut.SendEmailAsync(
            "a@b.com",
            "S",
            "body",
            Guid.Empty,
            unsubscribeUrl: unsub);

        using var doc = JsonDocument.Parse(capturedBody!);
        var headers = doc.RootElement.GetProperty("headers");
        headers.GetProperty("List-Unsubscribe").GetString().Should().Be($"<{unsub}>");
        headers.GetProperty("List-Unsubscribe-Post").GetString().Should().Be("List-Unsubscribe=One-Click");
    }

    [Test]
    public async Task SendEmailAsync_NonSystemWithoutByok_ThrowsNoPlatformFallback()
    {
        var handler = new CaptureHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var sut = CreateSut(handler);
        var tenantOrg = Guid.CreateVersion7();

        var act = async () => await sut.SendEmailAsync("a@b.com", "S", "body", tenantOrg);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No platform fallback*");
    }

    [Test]
    public async Task SendEmailAsync_SystemTenantWithoutPlatformKey_ReturnsNullWithoutHttp()
    {
        var called = false;
        var handler = new CaptureHandler((_, _) =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var sut = CreateSut(handler, platformApiKey: "");
        var result = await sut.SendEmailAsync("a@b.com", "S", "body", Guid.Empty);

        result.Should().BeNull();
        called.Should().BeFalse();
    }

    private sealed class FixedHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FixedHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.resend.com/")
            };
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public CaptureHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
