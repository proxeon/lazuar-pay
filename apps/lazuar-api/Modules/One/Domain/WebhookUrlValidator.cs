// apps/lazuar-api/Modules/One/Domain/WebhookUrlValidator.cs
using System;

namespace Modules.One.Domain;

/// <summary>
/// Shared absolute HTTPS (or loopback HTTP) validation for outbound webhook receiver URLs.
/// </summary>
public static class WebhookUrlValidator
{
    public const int MaxLength = 2048;

    /// <summary>
    /// Normalize and validate a webhook URL. Throws <see cref="InvalidOperationException"/> on invalid input.
    /// </summary>
    /// <param name="raw">Caller-supplied URL.</param>
    /// <param name="allowHttpLoopback">When true, allow http://localhost and http://127.0.0.1 for local/dev.</param>
    /// <returns>Trimmed URL string (original form, not re-serialized).</returns>
    public static string NormalizeAndValidate(string? raw, bool allowHttpLoopback = true)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("webhook_url is required when provided.");
        }

        var url = raw.Trim();
        if (url.Length > MaxLength)
        {
            throw new InvalidOperationException($"webhook_url must be at most {MaxLength} characters.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("webhook_url must be an absolute URL.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("webhook_url must not include user credentials.");
        }

        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);

        if (isHttps)
        {
            return url;
        }

        if (isHttp && allowHttpLoopback && IsLoopbackHost(uri.Host))
        {
            return url;
        }

        if (isHttp)
        {
            throw new InvalidOperationException(
                "webhook_url must use https (http is only allowed for localhost/127.0.0.1).");
        }

        throw new InvalidOperationException("webhook_url must use https scheme.");
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
}
