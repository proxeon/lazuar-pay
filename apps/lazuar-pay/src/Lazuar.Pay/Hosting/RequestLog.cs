using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace Lazuar.Pay.Hosting;

internal static class RequestLog
{
    public const string OrgItemKey = "pay.org_id";

    public static IApplicationBuilder UsePayRequestLog(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            var incoming = context.Request.Headers["X-Request-Id"].ToString().Trim();
            var requestId = string.IsNullOrWhiteSpace(incoming) ? context.TraceIdentifier : incoming;
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
}
