using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Entities;

public class MessageTemplate : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; private set; } = "";
    public string Channel { get; private set; } = "ALL";
    public string Subject { get; private set; } = "";
    public string EmailBody { get; private set; } = "";
    public string WhatsAppBody { get; private set; } = "";
    public bool IsDefault { get; private set; }

    private readonly List<string> _requiredVariables = new();
    public IReadOnlyCollection<string> RequiredVariables => _requiredVariables.AsReadOnly();

    private readonly List<string> _optionalVariables = new();
    public IReadOnlyCollection<string> OptionalVariables => _optionalVariables.AsReadOnly();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private MessageTemplate() { }
#pragma warning restore CS8618

    public MessageTemplate(
        Guid organizationId, string name, string channel,
        string subject, string emailBody, string whatsappBody, bool isDefault,
        IEnumerable<string>? requiredVariables = null, IEnumerable<string>? optionalVariables = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name;
        Channel = channel;
        Subject = subject;
        EmailBody = emailBody;
        WhatsAppBody = whatsappBody;
        IsDefault = isDefault;

        if (requiredVariables != null) _requiredVariables.AddRange(requiredVariables);
        if (optionalVariables != null) _optionalVariables.AddRange(optionalVariables);

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateContent(string subject, string emailBody, string whatsappBody)
    {
        Subject = subject;
        EmailBody = emailBody;
        WhatsAppBody = whatsappBody;
        IsDefault = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetToDefault(string subject, string emailBody, string whatsappBody, IEnumerable<string> requiredVariables, IEnumerable<string> optionalVariables)
    {
        Subject = subject;
        EmailBody = emailBody;
        WhatsAppBody = whatsappBody;
        IsDefault = true;

        _requiredVariables.Clear();
        if (requiredVariables != null) _requiredVariables.AddRange(requiredVariables);

        _optionalVariables.Clear();
        if (optionalVariables != null) _optionalVariables.AddRange(optionalVariables);

        UpdatedAt = DateTime.UtcNow;
    }
}
