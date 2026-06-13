// apps/lazuar-api/Modules/Community/Application/Commands/MessageTemplateCommands.cs
using System;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Rewrite the copy for automated emails and WhatsApp messages.", "COMMUNITY", "medium", "SUPER_ADMIN", "ADMIN")]
public record UpdateMessageTemplateCommand(Guid OrganizationId, Guid TemplateId, string Subject, string Body) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record ResetMessageTemplateCommand(Guid OrganizationId, Guid TemplateId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record SendTestReminderCommand(Guid OrganizationId, string TemplateName, string? Channel) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
