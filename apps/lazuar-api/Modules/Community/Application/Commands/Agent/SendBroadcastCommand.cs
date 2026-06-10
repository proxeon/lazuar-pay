using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application.Commands.Agent;

[AgentTool("Schedule a bulk announcement to be sent to all active subscribers or a specific plan. Returns a Campaign ID.", "high", "SUPER_ADMIN")]
public record SendBroadcastCommand(
    Guid OrganizationId, 
    string Subject, 
    string Body, 
    Guid? TargetPlanId) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class SendBroadcastCommandHandler : ICommandHandler<SendBroadcastCommand, string>
{
    private readonly IBroadcastCampaignRepository _repository;

    public SendBroadcastCommandHandler(IBroadcastCampaignRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> Handle(SendBroadcastCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new InvalidOperationException("Subject cannot be empty.");
            
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new InvalidOperationException("Body cannot be empty.");

        var campaign = new BroadcastCampaign(
            request.OrganizationId,
            request.Subject,
            request.Body,
            request.TargetPlanId);

        _repository.Add(campaign);
        await _repository.SaveChangesAsync(ct);

        return $"Broadcast campaign scheduled successfully. Campaign ID: {campaign.Id}. It will be processed shortly.";
    }
}
