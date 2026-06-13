using BuildingBlocks.Domain;

namespace Modules.Ops.Domain;

public class OpsMessage : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid ConversationId { get; private set; }
    public string Role { get; private set; }
    public string Content { get; private set; }
    public string? ExecutedToolsJson { get; private set; }
    public string? ProposedActionJson { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private OpsMessage() { }
#pragma warning restore CS8618

    public OpsMessage(Guid id, Guid organizationId, Guid conversationId, string role, string content, string? executedToolsJson = null, string? proposedActionJson = null)
    {
        Id = id;
        OrganizationId = organizationId;
        ConversationId = conversationId;
        Role = role.ToLowerInvariant();
        Content = content;
        ExecutedToolsJson = executedToolsJson;
        ProposedActionJson = proposedActionJson;
        CreatedAt = DateTime.UtcNow;
    }
}
