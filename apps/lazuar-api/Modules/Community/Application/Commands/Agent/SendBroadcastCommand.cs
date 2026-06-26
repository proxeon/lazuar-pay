using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application.Commands.Agent;

[AgentTool("Schedule a bulk announcement to be sent to multiple subscribers or an entire plan. ALWAYS use this tool instead of SendOneOffReminder when messaging multiple users or an entire plan.", "COMMUNITY", "high", "SUPER_ADMIN")]
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

public class SendBroadcastCommandHandler : ICommandHandler<SendBroadcastCommand, Guid>
{
    private readonly IBroadcastCampaignRepository _repository;

    public SendBroadcastCommandHandler(IBroadcastCampaignRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(SendBroadcastCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new InvalidOperationException("Subject cannot be empty.");

        if (string.IsNullOrWhiteSpace(request.EmailBody) && string.IsNullOrWhiteSpace(request.WhatsAppBody))
            throw new InvalidOperationException("Message body cannot be empty.");

        var campaign = new BroadcastCampaign(
            request.OrganizationId,
            request.Subject,
            request.EmailBody,
            request.WhatsAppBody,
            request.Channel,
            request.TargetPlanId,
            request.TargetStatus,
            request.TargetIsReminderOnly);

        _repository.Add(campaign);
        await _repository.SaveChangesAsync(ct);

        return campaign.Id;
    }
}
