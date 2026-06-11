// apps/lazuar-api/Modules/Community/Application/Commands/Agent/AgentSendCheckoutLinkCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Messaging.Contracts;
using Modules.One.Contracts;

namespace Modules.Community.Application.Commands.Agent;

[AgentTool("Generate and send a payment checkout link to a customer.", "medium", "SUPER_ADMIN", "ADMIN")]
public record AgentSendCheckoutLinkCommand(Guid OrganizationId, string Email, string PlanId, string CustomerName) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class AgentSendCheckoutLinkCommandHandler : ICommandHandler<AgentSendCheckoutLinkCommand>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly IOneQueryService _oneQueryService;
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;

    public AgentSendCheckoutLinkCommandHandler(
        ICommunityPlanRepository planRepository,
        IOneQueryService oneQueryService,
        IMediator mediator,
        [FromKeyedServices("CommunityEventBus")] IEventBus eventBus)
    {
        _planRepository = planRepository;
        _oneQueryService = oneQueryService;
        _mediator = mediator;
        _eventBus = eventBus;
    }

    public async Task Handle(AgentSendCheckoutLinkCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.PlanId, out var planGuid))
            throw new InvalidOperationException("Invalid Plan ID format.");

        var plan = await _planRepository.GetByIdAsync(planGuid, ct);
        if (plan == null || plan.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Plan not found.");

        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(request.OrganizationId);
        if (workspace == null)
            throw new InvalidOperationException("Workspace not found.");

        var registerCommand = new RegisterPublicSubscriberCommand(
            request.OrganizationId,
            workspace.Slug,
            plan.Slug,
            request.CustomerName,
            request.Email,
            "",
            null
        );

        var checkoutUrl = await _mediator.Send(registerCommand, ct);

        var emailSubject = $"Complete your registration for {plan.Name}";
        var emailBody = $"Hi {request.CustomerName},<br><br>Here is your personalized checkout link for {plan.Name}:<br><a href=\"{checkoutUrl}\">Complete Payment</a>";

        await _eventBus.PublishAsync(new DispatchMessageIntegrationEvent(
            request.OrganizationId,
            request.Email,
            null,
            emailSubject,
            emailBody,
            "EMAIL"
        ));
    }
}
