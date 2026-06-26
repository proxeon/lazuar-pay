// apps/lazuar-api/Modules/Community/Domain/Aggregates/CommunityPlan.cs
using System;
using BuildingBlocks.Domain;
using Modules.Community.Domain.Events;
using Modules.Community.Domain.Rules;

namespace Modules.Community.Domain.Aggregates;

public class CommunityPlan : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }

    public string Slug { get; private set; }
    public string Name { get; private set; }
    public string Audience { get; private set; }
    public decimal Price { get; private set; }
    public string Interval { get; private set; }
    public string PricingModel { get; private set; }
    public string? AdminNotes { get; private set; }

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
        decimal price, string interval, int gracePeriodDays, int? maxCapacity, 
        int displayOrder, string? adminNotes = null, string pricingModel = "FLAT_RATE")
    {
        CheckRule(new GracePeriodMustBePositiveRule(gracePeriodDays));

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Slug = slug;
        Name = name;
        Audience = audience;
        Price = price;
        Interval = interval;
        PricingModel = string.IsNullOrWhiteSpace(pricingModel) ? "FLAT_RATE" : pricingModel.ToUpperInvariant();
        GracePeriodDays = gracePeriodDays;
        MaxCapacity = maxCapacity;
        DisplayOrder = displayOrder;
        AdminNotes = adminNotes;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(
        string name, string audience, decimal price, string interval, 
        int gracePeriodDays, int? maxCapacity, int displayOrder, 
        bool isActive, string? adminNotes = null, string? pricingModel = null)
    {
        CheckRule(new GracePeriodMustBePositiveRule(gracePeriodDays));

        Name = name;
        Audience = audience;
        Price = price;
        Interval = interval;
        if (!string.IsNullOrWhiteSpace(pricingModel)) 
        {
            PricingModel = pricingModel.ToUpperInvariant();
        }
        GracePeriodDays = gracePeriodDays;
        MaxCapacity = maxCapacity;
        DisplayOrder = displayOrder;
        IsActive = isActive;
        AdminNotes = adminNotes;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PlanUpdatedDomainEvent(Id, OrganizationId, Slug, Name, Price));
    }

    public void SetFulfillmentLinks(string? telegramLink, string? meetingLink)
    {
        TelegramInviteLink = telegramLink;
        WeeklyMeetingLink = meetingLink;
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

        AddDomainEvent(new PlanArchivedDomainEvent(Id, OrganizationId, Slug));
    }
}
