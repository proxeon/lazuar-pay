using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.CRM.Contracts;

namespace Modules.Community.Application.Commands.Agent;

[AgentTool("Permanently erase a user's PII (Personally Identifiable Information) from the CRM and cancel their subscriptions to comply with GDPR/PDPA deletion requests.", "COMMUNITY", "high", "SUPER_ADMIN")]
public record AnonymizeSubscriberCommand(Guid OrganizationId, Guid ClientProfileId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class AnonymizeSubscriberCommandHandler : ICommandHandler<AnonymizeSubscriberCommand>
{
    private readonly ICrmQueryService _crmQueryService;
    private readonly IMediator _mediator;

    public AnonymizeSubscriberCommandHandler(
        ICrmQueryService crmQueryService,
        IMediator mediator)
    {
        _crmQueryService = crmQueryService;
        _mediator = mediator;
    }

    public async Task Handle(AnonymizeSubscriberCommand request, CancellationToken cancellationToken)
    {
        var profile = await _crmQueryService.GetClientProfileAsync(request.ClientProfileId);

        if (profile == null)
        {
            throw new InvalidOperationException("Client profile not found.");
        }

        var command = new AnonymizeClientProfileCommand(request.OrganizationId, request.ClientProfileId);
        await _mediator.Send(command, cancellationToken);
    }
}
