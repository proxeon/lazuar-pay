using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Domain.Aggregates;

public class DunningCampaign : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }
    public string FinalAction { get; private set; } 
    public int GracePeriodDays { get; private set; }

    private readonly List<Guid> _targetProductIds = new();
    public IReadOnlyCollection<Guid> TargetProductIds => _targetProductIds.AsReadOnly();

    private readonly List<string> _targetPaymentMethods = new();
    public IReadOnlyCollection<string> TargetPaymentMethods => _targetPaymentMethods.AsReadOnly();

    private readonly List<DunningStep> _steps = new();
    public IReadOnlyCollection<DunningStep> Steps => _steps.AsReadOnly();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private DunningCampaign() { }
#pragma warning restore CS8618

    public DunningCampaign(Guid organizationId, string name, string finalAction, int gracePeriodDays, IEnumerable<Guid>? targetProductIds = null, IEnumerable<string>? targetPaymentMethods = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name.Trim();
        IsActive = true;
        FinalAction = string.IsNullOrWhiteSpace(finalAction) ? "NONE" : finalAction.ToUpperInvariant();
        GracePeriodDays = gracePeriodDays;

        if (targetProductIds != null) _targetProductIds.AddRange(targetProductIds);
        if (targetPaymentMethods != null) _targetPaymentMethods.AddRange(targetPaymentMethods);

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string finalAction, int gracePeriodDays, IEnumerable<Guid>? targetProductIds, IEnumerable<string>? targetPaymentMethods)
    {
        Name = name.Trim();
        FinalAction = string.IsNullOrWhiteSpace(finalAction) ? "NONE" : finalAction.ToUpperInvariant();
        GracePeriodDays = gracePeriodDays;

        _targetProductIds.Clear();
        if (targetProductIds != null) _targetProductIds.AddRange(targetProductIds);

        _targetPaymentMethods.Clear();
        if (targetPaymentMethods != null) _targetPaymentMethods.AddRange(targetPaymentMethods);

        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearSteps()
    {
        _steps.Clear();
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddStep(int dayOffset, Guid templateId, string channel)
    {
        _steps.Add(new DunningStep(Id, dayOffset, templateId, channel));
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
