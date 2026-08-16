using System;
using System.Text.RegularExpressions;
using BuildingBlocks.Domain;
using Modules.One.Domain.Events;
using Modules.One.Domain.Rules;

namespace Modules.One.Domain;

public class Organization : Entity, IAggregateRoot
{
    /// <summary>Product tag for external org mapping (e.g. <c>aura</c>). Null when not provisioned by an integrator.</summary>
    public string? ExternalProduct { get; private set; }

    /// <summary>External product's org id (normalized). Unique with <see cref="ExternalProduct"/>.</summary>
    public string? ExternalOrgId { get; private set; }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    /// <summary>Optional https logo for hosted checkout. Not the billing-profile legal logo.</summary>
    public string? LogoUrl { get; private set; }
    /// <summary>Optional <c>#RRGGBB</c> accent for hosted checkout CTA.</summary>
    public string? PrimaryColor { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private Organization() { }
#pragma warning restore CS8618

    public Organization(string name, string slug)
    {
        var cleanSlug = slug.Trim().ToLowerInvariant();
        CheckRule(new OrganizationSlugMustBeValidRule(cleanSlug));

        Id = Guid.CreateVersion7();
        Name = name.Trim();
        Slug = cleanSlug;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrganizationCreatedDomainEvent(Id, Name, Slug));
    }

    public void UpdateDetails(string name, string slug)
    {
        var cleanSlug = slug.Trim().ToLowerInvariant();

        if (Slug != cleanSlug)
        {
            CheckRule(new OrganizationSlugMustBeValidRule(cleanSlug));
        }

        Name = name.Trim();
        Slug = cleanSlug;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrganizationUpdatedDomainEvent(Id, Name, Slug));
    }

    /// <summary>Set or clear checkout branding. Empty / null clears. Does not raise workspace-updated (name/slug only).</summary>
    public void UpdateBranding(string? logoUrl, string? primaryColor)
    {
        LogoUrl = NormalizeLogoUrl(logoUrl);
        PrimaryColor = NormalizePrimaryColor(primaryColor);
        UpdatedAt = DateTime.UtcNow;
    }

    public static string? NormalizeLogoUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("logo_url must be an http(s) URL.");
        }

        return uri.ToString();
    }

    public static string? NormalizePrimaryColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (!Regex.IsMatch(trimmed, "^#[0-9A-Fa-f]{6}$"))
        {
            throw new InvalidOperationException("primary_color must be #RRGGBB.");
        }

        return trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// Bind once to an external product org id. Idempotent if the same pair is re-applied;
    /// throws if already bound to a different product/id.
    /// </summary>
    public void BindExternalRef(string product, string externalOrgId)
    {
        if (string.IsNullOrWhiteSpace(product))
        {
            throw new InvalidOperationException("External product is required.");
        }

        if (string.IsNullOrWhiteSpace(externalOrgId))
        {
            throw new InvalidOperationException("External org id is required.");
        }

        var cleanProduct = product.Trim().ToLowerInvariant();
        var cleanExternalOrgId = externalOrgId.Trim().ToLowerInvariant();

        if (ExternalProduct is not null || ExternalOrgId is not null)
        {
            if (string.Equals(ExternalProduct, cleanProduct, StringComparison.Ordinal)
                && string.Equals(ExternalOrgId, cleanExternalOrgId, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Organization is already bound to external ref ({ExternalProduct}, {ExternalOrgId}).");
        }

        ExternalProduct = cleanProduct;
        ExternalOrgId = cleanExternalOrgId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrganizationArchivedDomainEvent(Id));
    }
}
