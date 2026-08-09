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
    public static string DefaultKeyNameFor(string product) =>
        string.Equals(product, ProductAura, StringComparison.Ordinal)
            ? DefaultKeyName
            : $"{product} bootstrap";

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
    /// Product slug: lower-case [a-z][a-z0-9_-]* , default <see cref="ProductAura"/>.
    /// </summary>
    public static string NormalizeExternalProduct(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ProductAura;
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

        return product;
    }

    /// <summary>
    /// Resolves external org id. For product <c>aura</c>, requires a GUID (legacy contract).
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
        if (string.Equals(product, ProductAura, StringComparison.Ordinal))
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
