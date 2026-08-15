using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Modules.Payments.Infrastructure.Gateways;

/// <summary>
/// Hop A stamp for Billplz. Public HTTPS callback vs sandbox/prod API host.
/// Do not use <c>Contains("lazuar.com")</c> — that would send pay-local.lazuar.com to production Billplz.
/// </summary>
internal static class BillplzPublicBase
{
    public const string CallbackBaseNotPublic = "CALLBACK_BASE_NOT_PUBLIC";

    private static readonly HashSet<string> ProductionHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.lazuar.com",
        "pay.lazuar.com",
        "hub.lazuar.com",
    };

    public static bool IsProductionApi(IConfiguration config, string? apiBaseUrl)
    {
        var explicitEnv = config["App:BillplzEnvironment"];
        if (string.Equals(explicitEnv, "production", StringComparison.OrdinalIgnoreCase)
            || string.Equals(explicitEnv, "live", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(explicitEnv, "sandbox", StringComparison.OrdinalIgnoreCase)
            || string.Equals(explicitEnv, "test", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Uri.TryCreate((apiBaseUrl ?? "").Trim(), UriKind.Absolute, out var uri))
            return false;
        return ProductionHosts.Contains(uri.Host);
    }

    public static bool AllowInsecureCallback(IConfiguration config) =>
        bool.TryParse(config["App:AllowInsecureBillplzCallback"], out var on) && on;

    public static bool TryResolveCallbackBase(
        IConfiguration config,
        string? apiBaseUrl,
        out string callbackBase,
        out string? error)
    {
        callbackBase = "";
        error = null;
        var raw = (apiBaseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = $"{CallbackBaseNotPublic}: App:ApiBaseUrl is empty.";
            return false;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            error = $"{CallbackBaseNotPublic}: App:ApiBaseUrl must be an absolute http(s) URL.";
            return false;
        }

        var host = uri.Host;
        var isLoopback = uri.IsLoopback
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        var isFiction = host.Contains("lazuar-local-dev.com", StringComparison.OrdinalIgnoreCase);

        if (AllowInsecureCallback(config))
        {
            callbackBase = raw;
            return true;
        }

        if (isLoopback || isFiction || uri.Scheme != Uri.UriSchemeHttps)
        {
            error =
                $"{CallbackBaseNotPublic}: set App:ApiBaseUrl to a public https origin Billplz can POST (Cloudflare tunnel), not localhost or lazuar-local-dev.com.";
            return false;
        }

        callbackBase = raw;
        return true;
    }
}
