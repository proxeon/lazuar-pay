using System;
using BuildingBlocks.Application;

namespace Modules.Communications.Contracts.Commands;

/// <summary>
/// Queue a marketing broadcast. v1 targets all ACTIVE/PAST_DUE marketing-consent subscribers.
/// Optional plan/status filters belong on a future command once fan-out supports them.
/// </summary>
public record SendBroadcastCommand(
    Guid OrganizationId,
    string Subject,
    string EmailBody,
    string WhatsAppBody,
    string Channel) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
