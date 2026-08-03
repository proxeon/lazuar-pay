using System;

namespace Modules.Lhdn.Domain;

/// <summary>
/// Scope constants for machine client (API key) authorization.
/// Stored space-separated on <see cref="Aggregates.DeveloperApiKey.Scopes"/>.
/// </summary>
public static class ApiKeyScopes
{
    public const string LhdnDocumentsRead = "lhdn.documents:read";
    public const string LhdnDocumentsWrite = "lhdn.documents:write";

    /// <summary>
    /// Default scopes granted to newly minted keys (v1 matrix).
    /// </summary>
    public const string DefaultDocumentScopes = LhdnDocumentsWrite + " " + LhdnDocumentsRead;

    /// <summary>
    /// Default for pre-scopes rows migrated from legacy keys (full document access).
    /// </summary>
    public const string LegacyDefaultScopes = DefaultDocumentScopes;

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
