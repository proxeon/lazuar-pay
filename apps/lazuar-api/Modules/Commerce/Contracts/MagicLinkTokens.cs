using System;

namespace Modules.Commerce.Contracts;

/// <summary>
/// Query-string encoding for portal HMAC tokens (B03-C17).
/// Generate is Base64url; still escape so a future alphabet change cannot break hrefs.
/// </summary>
public static class MagicLinkTokens
{
    public static string ToQueryValue(string token) => Uri.EscapeDataString(token);
}
