using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Contracts;

namespace Modules.Lhdn.Application.Commands;

/// <summary>
/// Obsolete LHDN-local command. Prefer <see cref="IApiCredentialService"/> (One platform credentials).
/// </summary>
[Obsolete("Platform credentials live in One. Use IApiCredentialService.RevokeAsync instead.")]
public record RevokeApiKeyCommand(Guid OrganizationId, Guid ApiKeyId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

#pragma warning disable CS0618 // Obsolete façade intentionally retained for callers not yet migrated
public class RevokeApiKeyCommandHandler : ICommandHandler<RevokeApiKeyCommand>
{
    private readonly IApiCredentialService _credentials;

    public RevokeApiKeyCommandHandler(IApiCredentialService credentials)
    {
        _credentials = credentials;
    }

    public Task Handle(RevokeApiKeyCommand request, CancellationToken ct)
    {
        return _credentials.RevokeAsync(request.OrganizationId, request.ApiKeyId, ct);
    }
}
#pragma warning restore CS0618
