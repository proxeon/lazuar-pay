using System;
using BuildingBlocks.Domain;

namespace Modules.One.Domain;

public class AuditEvent : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorEmail { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private AuditEvent() { }
#pragma warning restore CS8618

    public AuditEvent(
        Guid organizationId,
        string action,
        string entityType,
        string entityId,
        Guid? actorUserId = null,
        string? actorEmail = null,
        string? metadataJson = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ActorUserId = actorUserId == Guid.Empty ? null : actorUserId;
        ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? null : actorEmail.Trim().ToLowerInvariant();
        Action = action.Trim();
        EntityType = entityType.Trim();
        EntityId = entityId.Trim();
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson;
        CreatedAt = DateTime.UtcNow;
    }
}
