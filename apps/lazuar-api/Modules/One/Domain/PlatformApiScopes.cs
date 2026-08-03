using System;

namespace Modules.One.Domain;

/// <summary>
/// Scope constants for platform machine clients (API credentials).
/// Stored space-separated on <see cref="ApiCredential.Scopes"/>.
/// Product scopes (e.g. lhdn.*) live on platform keys per D3.
/// </summary>
public static class PlatformApiScopes
{
    public const string LhdnDocumentsRead = "lhdn.documents:read";
    public const string LhdnDocumentsWrite = "lhdn.documents:write";

    /// <summary>
    /// Default scopes granted to newly minted platform keys (v1 matrix).
    /// </summary>
    public const string DefaultDocumentScopes = LhdnDocumentsWrite + " " + LhdnDocumentsRead;

    public static string[] Split(string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes))
        {
            return [];
        }

        return scopes
            .Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static bool HasScope(string? scopes, string requiredScope)
    {
        foreach (var scope in Split(scopes))
        {
            if (string.Equals(scope, requiredScope, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
