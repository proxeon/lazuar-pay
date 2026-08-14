using System;
using System.Collections.Generic;
using System.Linq;

namespace Modules.One.Domain;

/// <summary>
/// Scope constants for platform machine clients (API credentials).
/// Stored space-separated on <see cref="ApiCredential.Scopes"/>.
/// Product scopes (e.g. lhdn.*, payments.*) live on platform keys.
/// </summary>
public static class PlatformApiScopes
{
    // --- LHDN documents ---
    public const string LhdnDocumentsRead = "lhdn.documents:read";
    public const string LhdnDocumentsWrite = "lhdn.documents:write";

    // --- Payments checkouts (Aura / M2M cashier) ---
    public const string PaymentsCheckoutsRead = "payments.checkouts:read";
    public const string PaymentsCheckoutsWrite = "payments.checkouts:write";

    // --- Optional catalog entries ---
    public const string PaymentsConfigRead = "payments.config:read";
    public const string WebhooksEndpointsManage = "webhooks.endpoints:manage";

    /// <summary>
    /// Default scopes when mint request omits scopes (backward-compatible LHDN matrix).
    /// </summary>
    public const string DefaultDocumentScopes = LhdnDocumentsWrite + " " + LhdnDocumentsRead;

    /// <summary>
    /// Suggested least-privilege bundle for Aura integrator keys (no LHDN).
    /// Includes webhook endpoint management so companion API works without remint.
    /// </summary>
    public const string DefaultAuraIntegratorScopes =
        PaymentsCheckoutsWrite + " " + PaymentsCheckoutsRead + " " + WebhooksEndpointsManage;

    /// <summary>
    /// Closed allowlist of mintable scopes (union of platform product scopes).
    /// </summary>
    public static readonly IReadOnlyList<string> AllKnownScopes =
    [
        LhdnDocumentsWrite,
        LhdnDocumentsRead,
        PaymentsCheckoutsWrite,
        PaymentsCheckoutsRead,
        PaymentsConfigRead,
        WebhooksEndpointsManage
    ];

    private static readonly HashSet<string> KnownScopeSet = new(AllKnownScopes, StringComparer.Ordinal);

    public static bool IsKnownScope(string? scope) =>
        !string.IsNullOrWhiteSpace(scope) && KnownScopeSet.Contains(scope.Trim());

    /// <summary>
    /// Normalize and validate requested scopes.
    /// <list type="bullet">
    /// <item><description><c>null</c> / omitted → <see cref="DefaultDocumentScopes"/> (LHDN compat)</description></item>
    /// <item><description>empty after trim → reject (never mint claimless keys)</description></item>
    /// <item><description>unknown string → reject with stable detail</description></item>
    /// </list>
    /// Returns space-separated string for storage.
    /// </summary>
    public static string NormalizeAndValidate(IEnumerable<string>? scopes)
    {
        if (scopes is null)
        {
            return DefaultDocumentScopes;
        }

        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in scopes)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var scope = raw.Trim();
            if (!seen.Add(scope))
            {
                continue;
            }

            if (!KnownScopeSet.Contains(scope))
            {
                throw new InvalidOperationException(
                    $"Unknown API scope: '{scope}'. Allowed scopes: {string.Join(", ", AllKnownScopes)}.");
            }

            list.Add(scope);
        }

        if (list.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one scope is required when scopes is provided. Omit scopes to use the LHDN document default.");
        }

        return string.Join(" ", list);
    }

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

    /// <summary>
    /// True when the key has any payments.* product scope (not webhook manage).
    /// </summary>
    public static bool HasAnyPaymentsScope(string? scopes) =>
        HasScope(scopes, PaymentsCheckoutsWrite)
        || HasScope(scopes, PaymentsCheckoutsRead)
        || HasScope(scopes, PaymentsConfigRead);
}
