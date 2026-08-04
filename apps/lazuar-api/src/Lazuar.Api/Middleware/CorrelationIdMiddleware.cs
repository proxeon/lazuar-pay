using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Lazuar.Api.Middleware;

/// <summary>
/// Accepts or generates <c>X-Correlation-Id</c>, stores it on the request,
/// echoes it on the response, and enriches the logging scope.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";
    public const string LogScopeKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [LogScopeKey] = correlationId
        }))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// Prefer inbound header when present and non-empty; otherwise generate a new Guid string.
    /// </summary>
    public static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            var candidate = headerValues.ToString().Trim();
            if (!string.IsNullOrEmpty(candidate) && candidate.Length <= 128)
            {
                return candidate;
            }
        }

        return Guid.CreateVersion7().ToString("D");
    }

    public static string? GetCorrelationId(HttpContext context)
        => context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
}
