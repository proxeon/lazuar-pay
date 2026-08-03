// apps/lazuar-api/Modules/One/Domain/TenantWebhookEndpoint.cs
using System;
using System.Collections.Generic;
using System.Linq;
using BuildingBlocks.Domain;

namespace Modules.One.Domain;

public class TenantWebhookEndpoint : Entity, IAggregateRoot, IMustHaveTenant
{
    private readonly List<string> _enabledEvents = new();

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Url { get; private set; }
    public string SecretKey { get; private set; }
    public bool IsActive { get; private set; }
    /// <summary>
    /// Event type filters (e.g. subscription.activated). Empty = accept all events.
    /// </summary>
    public IReadOnlyCollection<string> EnabledEvents => _enabledEvents.AsReadOnly();
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TenantWebhookEndpoint() { }
#pragma warning restore CS8618

    public TenantWebhookEndpoint(
        Guid organizationId,
        string url,
        string secretKey,
        bool isActive = true,
        IEnumerable<string>? enabledEvents = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Url = url;
        SecretKey = secretKey;
        IsActive = isActive;
        SetEnabledEvents(enabledEvents);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string url, bool isActive, IEnumerable<string>? enabledEvents = null)
    {
        Url = url;
        IsActive = isActive;
        if (enabledEvents != null)
        {
            SetEnabledEvents(enabledEvents);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Empty EnabledEvents means the endpoint accepts every event type.
    /// </summary>
    public bool AcceptsEvent(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return false;
        }

        if (_enabledEvents.Count == 0)
        {
            return true;
        }

        return _enabledEvents.Contains(eventType, StringComparer.OrdinalIgnoreCase);
    }

    private void SetEnabledEvents(IEnumerable<string>? enabledEvents)
    {
        _enabledEvents.Clear();
        if (enabledEvents == null)
        {
            return;
        }

        foreach (var e in enabledEvents
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _enabledEvents.Add(e);
        }
    }
}
