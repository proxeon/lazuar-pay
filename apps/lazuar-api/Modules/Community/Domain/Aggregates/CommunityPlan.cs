using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;
using Modules.Community.Domain.Events;
using Modules.Community.Domain.Rules;
using Modules.Community.Domain.ValueObjects;

namespace Modules.Community.Domain.Aggregates;

public class CommunityPlan : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }

    public string Slug { get; private set; }
    public string Name { get; private set; }
    public string Audience { get; private set; }
    public string ShortDescription { get; private set; }
    public string LongDescription { get; private set; }
    public decimal Price { get; private set; }
    public string Interval { get; private set; }
    
    private readonly List<string> _features = new();
    public IReadOnlyCollection<string> Features => _features.AsReadOnly();
    
    public string Methodology { get; private set; }
    
    private readonly List<FaqItem> _faq = new();
    public IReadOnlyCollection<FaqItem> Faq => _faq.AsReadOnly();

    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public int? MaxCapacity { get; private set; }
    public int GracePeriodDays { get; private set; }

    public string? TelegramInviteLink { get; private set; }
    public string? WeeklyMeetingLink { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private CommunityPlan() { }
#pragma warning restore CS8618

    public CommunityPlan(
        Guid organizationId, string slug, string name, string audience, 
        string shortDescription, string longDescription, decimal price, 
        string interval, int gracePeriodDays, int? maxCapacity, int displayOrder, 
        string methodology)
    {
        CheckRule(new GracePeriodMustBePositiveRule(gracePeriodDays));

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Slug = slug;
        Name = name;
        Audience = audience;
        ShortDescription = shortDescription;
        LongDescription = longDescription;
        Price = price;
        Interval = interval;
        GracePeriodDays = gracePeriodDays;
        MaxCapacity = maxCapacity;
        DisplayOrder = displayOrder;
        Methodology = methodology;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(
        string name, string audience, string shortDesc, string longDesc, 
        decimal price, string interval, int gracePeriodDays, int? maxCapacity, 
        int displayOrder, bool isActive, string methodology)
    {
        CheckRule(new GracePeriodMustBePositiveRule(gracePeriodDays));

        Name = name;
        Audience = audience;
        ShortDescription = shortDesc;
        LongDescription = longDesc;
        Price = price;
        Interval = interval;
        GracePeriodDays = gracePeriodDays;
        MaxCapacity = maxCapacity;
        DisplayOrder = displayOrder;
        IsActive = isActive;
        Methodology = methodology;
        UpdatedAt = DateTime.UtcNow;

        // Trigger decoupled audit log
        AddDomainEvent(new PlanUpdatedDomainEvent(Id, OrganizationId, Slug, Name, Price));
    }

    public void SetFulfillmentLinks(string? telegramLink, string? meetingLink)
    {
        TelegramInviteLink = telegramLink;
        WeeklyMeetingLink = meetingLink;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateFeatures(IEnumerable<string> features)
    {
        _features.Clear();
        _features.AddRange(features);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateFaq(IEnumerable<FaqItem> faqs)
    {
        _faq.Clear();
        _faq.AddRange(faqs);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSlug(string uniqueSlug)
    {
        Slug = uniqueSlug;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        // Trigger decoupled audit log
        AddDomainEvent(new PlanArchivedDomainEvent(Id, OrganizationId, Slug));
    }
}
