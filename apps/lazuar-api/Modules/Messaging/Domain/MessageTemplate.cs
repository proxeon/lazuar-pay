using System;
using BuildingBlocks.Domain;

namespace Modules.Messaging.Domain;

public class MessageTemplate : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; private set; } = "";
    public string Channel { get; private set; } = "ALL";
    public string Subject { get; private set; } = "";
    public string Body { get; private set; } = "";
    public bool IsDefault { get; private set; }
    public string? MetaTemplateName { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    #pragma warning disable CS8618
    private MessageTemplate() { }
    #pragma warning restore CS8618

    public MessageTemplate(
        Guid organizationId, string name, string channel,
        string subject, string body, bool isDefault, string? metaTemplateName = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name;
        Channel = channel;
        Subject = subject;
        Body = body;
        IsDefault = isDefault;
        MetaTemplateName = metaTemplateName;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateContent(string subject, string body)
    {
        Subject = subject;
        Body = body;
        IsDefault = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetToDefault(string subject, string body)
    {
        Subject = subject;
        Body = body;
        IsDefault = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
