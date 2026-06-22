using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public record RegisterPublicUserCommand(string Email, string Password, string? Name) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RegisterPublicUserCommandHandler : ICommandHandler<RegisterPublicUserCommand, Guid>
{
    private readonly IOneRepository _repository;
    private readonly IPasswordService _passwordService;
    private readonly IMediator _mediator;

    public RegisterPublicUserCommandHandler(IOneRepository repository, IPasswordService passwordService, IMediator mediator)
    {
        _repository = repository;
        _passwordService = passwordService;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(RegisterPublicUserCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _repository.GetUserByEmailAsync(email, ct);
        if (existingUser != null)
        {
            throw new InvalidOperationException("An account with this email address already exists.");
        }

        var passwordHash = _passwordService.Hash(request.Password);
        var name = string.IsNullOrWhiteSpace(request.Name) ? email.Split('@')[0] : request.Name.Trim();

        var user = new GlobalUser(email, name, passwordHash, isSystemAdmin: false);
        _repository.AddGlobalUser(user);
        await _repository.SaveChangesAsync(ct);

        // Auto-provision a workspace if they are not signing up via an active invitation
        bool hasPendingInvite = await _repository.HasPendingInvitationAsync(email, ct);
        if (!hasPendingInvite)
        {
            var baseSlug = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]", "-").Trim('-');
            if (string.IsNullOrEmpty(baseSlug)) baseSlug = "workspace";
            var uniqueSlug = $"{baseSlug}-{Guid.NewGuid().ToString()[..4]}";

            await _mediator.Send(new CreateWorkspaceCommand(
                user.Id,
                $"{name}'s Workspace",
                uniqueSlug,
                new List<string> { "COMMUNITY", "OPS", "BILLING" }
            ), ct);
        }

        return user.Id;
    }
}
