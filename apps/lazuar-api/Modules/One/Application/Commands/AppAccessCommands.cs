using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record RequestAppAccessCommand(Guid UserId, List<string> RequestedApps) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RequestAppAccessCommandHandler : ICommandHandler<RequestAppAccessCommand, Guid>
{
    private readonly IOneRepository _repository;

    public RequestAppAccessCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(RequestAppAccessCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserByIdAsync(request.UserId, ct);
        if (user == null || !user.IsActive) throw new InvalidOperationException("Invalid user session.");

        var accessRequest = new AppAccessRequest(user.Id, request.RequestedApps);
        _repository.AddAppAccessRequest(accessRequest);
        
        await _repository.SaveChangesAsync(ct);
        return accessRequest.Id;
    }
}

public record ApproveAppAccessCommand(Guid RequestId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ApproveAppAccessCommandHandler : ICommandHandler<ApproveAppAccessCommand>
{
    private readonly IOneRepository _repository;
    private readonly IMediator _mediator;

    public ApproveAppAccessCommandHandler(IOneRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task Handle(ApproveAppAccessCommand request, CancellationToken ct)
    {
        var accessReq = await _repository.GetAppAccessRequestByIdAsync(request.RequestId, ct);
        if (accessReq == null) throw new InvalidOperationException("Request not found.");

        accessReq.Approve();

        var user = await _repository.GetUserByIdAsync(accessReq.GlobalUserId, ct);
        if (user == null) throw new InvalidOperationException("User not found.");

        var baseSlug = Regex.Replace(user.Name.ToLowerInvariant(), @"[^a-z0-9]", "-").Trim('-');
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "workspace";
        var uniqueSlug = $"{baseSlug}-{Guid.NewGuid().ToString()[..4]}";

        // Defer to the existing CreateWorkspaceCommand to handle entitlements and events securely
        var workspaceCommand = new CreateWorkspaceCommand(
            user.Id,
            $"{user.Name}'s Workspace",
            uniqueSlug,
            accessReq.RequestedApps
        );

        await _mediator.Send(workspaceCommand, ct);
        await _repository.SaveChangesAsync(ct);
    }
}

public record RejectAppAccessCommand(Guid RequestId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RejectAppAccessCommandHandler : ICommandHandler<RejectAppAccessCommand>
{
    private readonly IOneRepository _repository;

    public RejectAppAccessCommandHandler(IOneRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(RejectAppAccessCommand request, CancellationToken ct)
    {
        var accessReq = await _repository.GetAppAccessRequestByIdAsync(request.RequestId, ct);
        if (accessReq == null) throw new InvalidOperationException("Request not found.");

        accessReq.Reject();
        await _repository.SaveChangesAsync(ct);
    }
}
