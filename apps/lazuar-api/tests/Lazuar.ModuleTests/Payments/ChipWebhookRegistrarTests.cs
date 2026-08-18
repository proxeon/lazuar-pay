using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Modules.Payments.Infrastructure.Gateways;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class ChipWebhookRegistrarTests
{
    private const string Callback = "https://api.test/webhooks/payments/chip/org";
    private const string WebhookPem = "-----BEGIN PUBLIC KEY-----\nWEBHOOK\n-----END PUBLIC KEY-----";

    [Test]
    public async Task EnsureRegistered_ExistingCallback_DoesNotPostAgain()
    {
        var handler = new ScriptedHandler();
        handler.On(HttpMethod.Get, ChipWebhookRegistrar.WebhooksUrl, _ => Json(
            $$"""{"results":[{"id":"wh_1","callback":"{{Callback}}","public_key":"{{WebhookPem.Replace("\n", "\\n")}}"}]}"""));
        var client = new HttpClient(handler);

        var pem = await ChipWebhookRegistrar.EnsureRegisteredAsync(client, Callback, CancellationToken.None);

        pem.Should().Contain("WEBHOOK");
        handler.Posts.Should().Be(0);
    }

    [Test]
    public async Task EnsureRegistered_MissingCallback_PostsOnce_UsesWebhookPublicKey()
    {
        var handler = new ScriptedHandler();
        handler.On(HttpMethod.Get, ChipWebhookRegistrar.WebhooksUrl, _ => Json("""{"results":[]}"""));
        handler.On(HttpMethod.Post, ChipWebhookRegistrar.WebhooksUrl, _ => Json(
            $$"""{"id":"wh_new","callback":"{{Callback}}","public_key":"{{WebhookPem.Replace("\n", "\\n")}}"}"""));
        var client = new HttpClient(handler);

        var pem = await ChipWebhookRegistrar.EnsureRegisteredAsync(client, Callback, CancellationToken.None);

        pem.Should().Contain("WEBHOOK");
        handler.Posts.Should().Be(1);
    }

    [Test]
    public void ExtractPublicKey_ReadsWebhookObject()
    {
        ChipWebhookRegistrar.ExtractPublicKey("""{"public_key":"-----BEGIN PUBLIC KEY-----\\nX\\n-----END PUBLIC KEY-----"}""")
            .Should().Contain("BEGIN PUBLIC KEY");
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly System.Collections.Generic.List<(HttpMethod Method, string Url, Func<HttpRequestMessage, HttpResponseMessage> Reply)> _routes = new();
        public int Posts { get; private set; }

        public void On(HttpMethod method, string url, Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            _routes.Add((method, url, reply));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                Posts++;
            }

            foreach (var route in _routes)
            {
                if (route.Method == request.Method
                    && string.Equals(request.RequestUri?.ToString(), route.Url, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(route.Reply(request));
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
