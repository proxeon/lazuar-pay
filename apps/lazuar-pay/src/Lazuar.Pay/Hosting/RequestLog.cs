using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Routing;

namespace Lazuar.Pay.Hosting;

internal static class RequestLog
{
    public const string OrgItemKey = "pay.org_id";

    /// <summary>
    /// plans/031/05: the audited actor for this request (the One user id), stashed by
    /// MemberGate in the same pass that resolves the org — audit rows read it from here
    /// instead of re-calling One.
    /// </summary>
    public const string ActorItemKey = "pay.actor_id";

    public static string? Actor(HttpRequest request) =>
        request.HttpContext.Items.TryGetValue(ActorItemKey, out var raw) ? raw as string : null;

    // Issue 007 (issues/003): the echoed id is capped so a caller-supplied megabyte header
    // cannot inflate the response head and the log line.
    const int MaxRequestIdLength = 64;

    public static IApplicationBuilder UsePayRequestLog(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            var incoming = context.Request.Headers["X-Request-Id"].ToString().Trim();
            // Issue 007 (issues/003): the value is caller-supplied and echoed into a
            // response header — Kestrel throws when the response head is written if the
            // value carries non-ASCII or control bytes, turning every such request
            // (health included) into a raw 500. Keep printable ASCII only and fall back to
            // the trace id when nothing survives.
            var requestId = SanitizeRequestId(incoming);
            if (requestId.Length == 0)
            {
                requestId = context.TraceIdentifier;
            }
            context.Response.Headers["X-Request-Id"] = requestId;
            try
            {
                await next();
            }
            finally
            {
                var durationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                            ?? context.Request.Path.Value;
                var org = context.Items.TryGetValue(OrgItemKey, out var raw) ? raw as string : null;
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Lazuar.Pay.Http");
                logger.LogInformation(
                    "http {RequestId} {Method} {Route} {Status} {DurationMs} {OrgId}",
                    requestId,
                    context.Request.Method,
                    route,
                    context.Response.StatusCode,
                    durationMs,
                    org);
            }
        });
        return app;
    }

    /// <summary>Printable ASCII only, length-capped; empty output means "use the trace id".</summary>
    static string SanitizeRequestId(string value)
    {
        if (value.Length > MaxRequestIdLength)
        {
            value = value[..MaxRequestIdLength];
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c >= ' ' && c <= '~')
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Trim();
    }
}
