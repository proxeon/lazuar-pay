using System;
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
