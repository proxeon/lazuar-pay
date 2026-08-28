using System.Net;
using System.Text;

namespace Lazuar.Pay.Tests;

public sealed class FakePspHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, string?, HttpResponseMessage>? Responder { get; set; }

    public string? LastBody { get; private set; }

    public Uri? LastUri { get; private set; }

    public int SendCount { get; private set; }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SendCount++;
        LastRequest = request;
        LastUri = request.RequestUri;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        if (Responder is not null)
        {
            return Responder(request, LastBody);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
    }
}

public sealed class StaticHttpFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(5) };
}
