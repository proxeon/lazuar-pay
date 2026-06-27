using System;
using BuildingBlocks.Application;

namespace Modules.Communications.Contracts.Commands;

public record SendBroadcastCommand(
    Guid OrganizationId,
    string Subject,
    string EmailBody,
    string WhatsAppBody,
    string Channel,
    Guid? TargetPlanId = null,
    string? TargetStatus = null,
    bool? TargetIsReminderOnly = null) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
