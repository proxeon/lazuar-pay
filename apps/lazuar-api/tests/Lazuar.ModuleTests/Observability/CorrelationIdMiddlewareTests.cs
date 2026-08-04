using System;
using System.Threading.Tasks;
using Lazuar.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Observability;

[TestFixture]
public class CorrelationIdMiddlewareTests
{
    [Test]
    public async Task Invoke_Uses_Incoming_X_Correlation_Id_Header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "client-corr-123";

        string? seenInItems = null;
        var middleware = new CorrelationIdMiddleware(
            next: ctx =>
            {
                seenInItems = CorrelationIdMiddleware.GetCorrelationId(ctx);
                return Task.CompletedTask;
            },
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.That(seenInItems, Is.EqualTo("client-corr-123"));
        Assert.That(context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString(), Is.EqualTo("client-corr-123"));
        Assert.That(context.Items[CorrelationIdMiddleware.ItemKey], Is.EqualTo("client-corr-123"));
    }

    [Test]
    public async Task Invoke_Generates_Guid_When_Header_Missing()
    {
        var context = new DefaultHttpContext();

        var middleware = new CorrelationIdMiddleware(
            next: _ => Task.CompletedTask,
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var id = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.That(Guid.TryParse(id, out _), Is.True);
        Assert.That(context.Items[CorrelationIdMiddleware.ItemKey], Is.EqualTo(id));
    }

    [Test]
    public void ResolveCorrelationId_Ignores_Whitespace_Only_Header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "   ";

        var id = CorrelationIdMiddleware.ResolveCorrelationId(context);
        Assert.That(Guid.TryParse(id, out _), Is.True);
    }
}
