// apps/lazuar-api/Modules/Community/Application/Commands/Agent/AgentSendMagicLinkCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.One.Contracts;

namespace Modules.Community.Application.Commands.Agent;

[AgentTool("Send a secure portal login magic link to a subscriber.", "medium", "SUPER_ADMIN", "ADMIN")]
public record AgentSendMagicLinkCommand(Guid OrganizationId, string Email) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class AgentSendMagicLinkCommandHandler : ICommandHandler<AgentSendMagicLinkCommand>
{
    private readonly IOneQueryService _oneQueryService;
    private readonly ICommunityLinkService _linkService;
    private readonly IMediator _mediator;

    public AgentSendMagicLinkCommandHandler(
        IOneQueryService oneQueryService,
        ICommunityLinkService linkService,
        IMediator mediator)
    {
        _oneQueryService = oneQueryService;
        _linkService = linkService;
        _mediator = mediator;
    }

    public async Task Handle(AgentSendMagicLinkCommand request, CancellationToken ct)
    {
        var workspace = await _oneQueryService.GetWorkspaceByIdAsync(request.OrganizationId);
        if (workspace == null)
            throw new InvalidOperationException("Workspace not found.");

        var baseUrl = _linkService.GetCommunityBaseUrl();

        var magicLinkCommand = new RequestMagicLinkCommand(
            request.OrganizationId,
            workspace.Slug,
            request.Email,
            baseUrl
        );

        await _mediator.Send(magicLinkCommand, ct);
    }
}
