// apps/lazuar-api/Modules/Ops/Domain/OpsMessage.cs
using System;
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
    public string? UiRequestJson { get; private set; }
    public bool IsResolved { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private OpsMessage() { }
#pragma warning restore CS8618

    public OpsMessage(
        Guid id, 
        Guid organizationId, 
        Guid conversationId, 
        string role, 
        string content, 
        string? executedToolsJson = null, 
        string? proposedActionJson = null,
        string? uiRequestJson = null,
        bool isResolved = false)
    {
        Id = id;
        OrganizationId = organizationId;
        ConversationId = conversationId;
        Role = role.ToLowerInvariant();
        Content = content;
        ExecutedToolsJson = executedToolsJson;
        ProposedActionJson = proposedActionJson;
        UiRequestJson = uiRequestJson;
        IsResolved = isResolved;
        CreatedAt = DateTime.UtcNow;
    }

    public void ResolveUiRequest()
    {
        IsResolved = true;
    }
}
