using System.Net;
using Lazuar.Pay.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Issue 007 (issues/003): X-Request-Id is caller-supplied and echoed into a response
/// header and the request log. Unsanitized, a non-ASCII value turned every request into a
/// raw 500 (Kestrel rejects non-ASCII/control response-header bytes when the head is
/// written), and an unbounded value fed the log line. These tests pin the sanitization
/// contract at the middleware level: printable ASCII only, length-capped, trace-id
/// fallback, and the endpoint must never fail because of the header.
/// </summary>
public class RequestLogTests
{
    static async Task<(int Status, string? Echoed, string TraceId)> Invoke(string? requestIdHeader)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var builder = new ApplicationBuilder(services);
        builder.UsePayRequestLog();
        builder.Run(_ => Task.CompletedTask);
        var pipeline = builder.Build();

        var context = new DefaultHttpContext();
        context.RequestServices = services;
        if (requestIdHeader is not null)
        {
            context.Request.Headers["X-Request-Id"] = requestIdHeader;
        }

        await pipeline(context);
        return (context.Response.StatusCode, context.Response.Headers["X-Request-Id"], context.TraceIdentifier);
    }

    static bool IsPrintableAscii(string? value) =>
        value is not null && value.All(c => c >= ' ' && c <= '~');

    [Test]
    public async Task Non_ascii_request_id_is_stripped_not_a_500()
    {
        var (status, echoed, _) = await Invoke("träck-123");
        Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(echoed, Is.EqualTo("trck-123"));
        Assert.That(IsPrintableAscii(echoed));
    }

    [Test]
    public async Task Control_bytes_are_stripped()
    {
        var (status, echoed, _) = await Invoke("abc\tdef\r\ninj");
        Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(echoed, Is.EqualTo("abcdefinj"));
    }

    [Test]
    public async Task Request_id_over_64_chars_is_capped()
    {
        var (status, echoed, _) = await Invoke(new string('a', 200));
        Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(echoed, Has.Length.EqualTo(64));
    }

    [Test]
    public async Task Only_invalid_bytes_falls_back_to_trace_id()
    {
        var (status, echoed, traceId) = await Invoke("中文");
        Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(echoed, Is.Not.Empty);
        Assert.That(echoed, Is.EqualTo(traceId));
    }

    [Test]
    public async Task Missing_header_falls_back_to_trace_id()
    {
        var (status, echoed, traceId) = await Invoke(null);
        Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(echoed, Is.EqualTo(traceId));
    }

    [Test]
    public async Task Clean_request_id_is_echoed_verbatim()
    {
        var (status, echoed, _) = await Invoke("evt-abc_123.X");
        Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
        Assert.That(echoed, Is.EqualTo("evt-abc_123.X"));
    }

    [Test]
    public async Task Health_endpoint_survives_an_oversized_header_end_to_end()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.TryAddWithoutValidation("X-Request-Id", new string('x', 500));
        var response = await client.SendAsync(req);
        Assert.That(response.IsSuccessStatusCode);
        Assert.That(response.Headers.TryGetValues("X-Request-Id", out var values) && IsPrintableAscii(values.FirstOrDefault()), Is.True);
        Assert.That(values?.FirstOrDefault()?.Length, Is.EqualTo(64));
    }
}
