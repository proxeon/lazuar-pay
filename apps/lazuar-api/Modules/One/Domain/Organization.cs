using System;
using BuildingBlocks.Domain;
using Modules.One.Domain.Events;
using Modules.One.Domain.Rules;

namespace Modules.One.Domain;

public class Organization : Entity, IAggregateRoot
{
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

    public void Archive()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrganizationArchivedDomainEvent(Id));
    }
}
