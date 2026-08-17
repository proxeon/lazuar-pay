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
    public int PriorityOrder { get; private set; }

    public decimal RecoveredRevenue { get; private set; }
    public int SavedSubscriptions { get; private set; }
    public int ChurnedSubscriptions { get; private set; }

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

    public DunningCampaign(Guid organizationId, string name, string finalAction, int gracePeriodDays, int priorityOrder = 0, IEnumerable<Guid>? targetProductIds = null, IEnumerable<string>? targetPaymentMethods = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name.Trim();
        IsActive = true;
        FinalAction = string.IsNullOrWhiteSpace(finalAction) ? "NONE" : finalAction.ToUpperInvariant();
        GracePeriodDays = gracePeriodDays;
        PriorityOrder = priorityOrder;

        RecoveredRevenue = 0;
        SavedSubscriptions = 0;
        ChurnedSubscriptions = 0;

        if (targetProductIds != null) _targetProductIds.AddRange(targetProductIds);
        if (targetPaymentMethods != null) _targetPaymentMethods.AddRange(targetPaymentMethods);

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string finalAction, int gracePeriodDays, int priorityOrder, IEnumerable<Guid>? targetProductIds, IEnumerable<string>? targetPaymentMethods)
    {
        Name = name.Trim();
        FinalAction = string.IsNullOrWhiteSpace(finalAction) ? "NONE" : finalAction.ToUpperInvariant();
        GracePeriodDays = gracePeriodDays;
        PriorityOrder = priorityOrder;

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

    public void AddStep(int dayOffset, string actionType, string? subject, string? emailBody, string? whatsAppBody)
    {
        if (_steps.Any(s => s.DayOffset == dayOffset))
        {
            throw new InvalidOperationException(
                $"A dunning campaign cannot have two steps on day offset {dayOffset}.");
        }

        _steps.Add(new DunningStep(Id, dayOffset, actionType, subject, emailBody, whatsAppBody));
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordRecovery(decimal amount)
    {
        RecoveredRevenue += amount;
        SavedSubscriptions++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordChurn()
    {
        ChurnedSubscriptions++;
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

    /// <summary>
    /// Empty target lists match any product/method. Caller still filters org and sorts by priority.
    /// </summary>
    public bool Matches(Guid organizationId, Guid productId, string paymentMethod)
    {
        if (OrganizationId != organizationId)
        {
            return false;
        }

        if (_targetProductIds.Count > 0 && !_targetProductIds.Contains(productId))
        {
            return false;
        }

        if (_targetPaymentMethods.Count > 0 && !_targetPaymentMethods.Contains(paymentMethod))
        {
            return false;
        }

        return true;
    }
}
