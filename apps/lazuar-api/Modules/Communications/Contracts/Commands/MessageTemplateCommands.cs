using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Communications.Contracts.Commands;

public record CreateMessageTemplateCommand(
    Guid OrganizationId, 
    string Name, 
    string Subject, 
    string EmailBody, 
    string WhatsAppBody, 
    string Channel, 
    IEnumerable<string> RequiredVariables, 
    IEnumerable<string> OptionalVariables) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record UpdateMessageTemplateCommand(
    Guid OrganizationId, 
    Guid TemplateId, 
    string Subject, 
    string EmailBody, 
    string WhatsAppBody) : ICommand
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
