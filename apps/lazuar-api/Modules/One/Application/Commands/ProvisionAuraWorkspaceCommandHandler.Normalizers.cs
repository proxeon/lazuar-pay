using System;
using System.Collections.Generic;
using System.Linq;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

/// <summary>
/// Pure normalizers / validators for provision inputs.
/// Kept as partial of the handler so public static call sites stay stable.
/// </summary>
public partial class ProvisionAuraWorkspaceCommandHandler
{
    public readonly record struct ProvisionIdentity(string Product, string ExternalOrgIdRaw);

    public static bool IsAuraProduct(string product) =>
        string.Equals(product, ProductAura, StringComparison.Ordinal)
        || string.Equals(product, ProductAurabook, StringComparison.Ordinal);

    public static string DefaultKeyNameFor(string product) =>
        IsAuraProduct(product)
            ? DefaultKeyName
            : $"{product} bootstrap";

    /// <summary>
    /// HTTP field-presence rules (P01.03). Command-level <see cref="NormalizeExternalProduct"/>
    /// still defaults empty → aura so existing handler tests stay valid.
    /// </summary>
    public static ProvisionIdentity ResolveProvisionIdentity(
        string? externalProduct,
        string? externalOrgId,
        string? auraOrgId)
    {
        var hasProduct = !string.IsNullOrWhiteSpace(externalProduct);
        var hasExternalOrg = !string.IsNullOrWhiteSpace(externalOrgId);

        if (!hasProduct && hasExternalOrg)
        {
            throw new InvalidOperationException(
                $"{ErrorExternalProductRequired}: external_product is required when external_org_id is sent.");
        }

        var product = hasProduct
            ? NormalizeExternalProduct(externalProduct)
            : ProductAura;

        var orgRaw = FirstNonEmpty(externalOrgId, auraOrgId);
        return new ProvisionIdentity(product, orgRaw);
    }

    public static string NormalizeOwnerRole(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultOwnerRole;
        }

        var role = raw.Trim().ToUpperInvariant();
        if (!AllowedOwnerRoles.Contains(role))
        {
            throw new InvalidOperationException(
                "owner_role must be ADMIN or SUPER_ADMIN (workspace membership, not global system admin).");
        }

        return role;
    }

    public static string? NormalizeOptionalWebhookUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return WebhookUrlValidator.NormalizeAndValidate(raw, allowHttpLoopback: true);
    }

    /// <summary>
    /// null/empty → Connect payment defaults; non-empty → caller filter.
    /// </summary>
    public static IReadOnlyList<string> ResolveWebhookEnabledEvents(IReadOnlyList<string>? events)
    {
        if (events is null || events.Count == 0)
        {
            return DefaultConnectWebhookEvents.ToList();
        }

        return events
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Product slug: lower-case [a-z][a-z0-9_-]* . Empty defaults to <see cref="ProductAura"/>
    /// (command-level / test compat only — HTTP must call <see cref="ResolveProvisionIdentity"/>).
    /// Alias <see cref="ProductAurabook"/> folds to stored <see cref="ProductAura"/>.
    /// </summary>
    public static string NormalizeExternalProduct(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ProductAura; // command-level / test compat only — HTTP must call ResolveProvisionIdentity
        }

        var product = raw.Trim().ToLowerInvariant();
        if (product.Length > MaxExternalProductLength)
        {
            throw new InvalidOperationException(
                $"external_product must be at most {MaxExternalProductLength} characters.");
        }

        if (!ProductSlugPattern.IsMatch(product))
        {
            throw new InvalidOperationException(
                "external_product must start with a letter and contain only a-z, 0-9, _ or -.");
        }

        return string.Equals(product, ProductAurabook, StringComparison.Ordinal)
            ? ProductAura
            : product;
    }

    /// <summary>
    /// Resolves external org id. For product <c>aura</c> (and alias <c>aurabook</c>), requires a GUID.
    /// Other products accept any non-empty stable string (max 128), lowercased.
    /// </summary>
    public static string NormalizeExternalOrgId(string? raw, string product)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "external_org_id (or aura_org_id) is required.");
        }

        var trimmed = raw.Trim();
        var folded = string.Equals(product, ProductAurabook, StringComparison.Ordinal)
            ? ProductAura
            : product;

        if (IsAuraProduct(folded))
        {
            return NormalizeAuraOrgId(trimmed);
        }

        if (trimmed.Length > MaxExternalOrgIdLength)
        {
            throw new InvalidOperationException(
                $"external_org_id must be at most {MaxExternalOrgIdLength} characters.");
        }

        return trimmed.ToLowerInvariant();
    }

    private static string FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a))
        {
            return a.Trim();
        }

        return string.IsNullOrWhiteSpace(b) ? string.Empty : b.Trim();
    }

    /// <summary>Aura-only GUID normalizer (legacy name kept for tests / callers).</summary>
    public static string NormalizeAuraOrgId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("aura_org_id is required.");
        }

        var trimmed = raw.Trim();
        if (!Guid.TryParse(trimmed, out var guid))
        {
            throw new InvalidOperationException("aura_org_id must be a valid GUID.");
        }

        return guid.ToString("D").ToLowerInvariant();
    }
}
