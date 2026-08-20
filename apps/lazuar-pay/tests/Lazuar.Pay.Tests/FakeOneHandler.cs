using System.Net;
using System.Text;

namespace Lazuar.Pay.Tests;

public sealed class FakeOneHandler : HttpMessageHandler
{
    public int SendCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }
    public bool ThrowOnSend { get; set; }
    public TimeSpan Delay { get; set; }
    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.OK);

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        SendCount++;
        LastRequest = request;
        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        if (ThrowOnSend)
        {
            throw new HttpRequestException("one down");
        }

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        return Responder(request);
    }
}
