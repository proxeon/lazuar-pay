using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.One.Contracts;

namespace Modules.Lhdn.Application.Commands;

public record GenerateApiKeyResult(
    Guid Id,
    string Name,
    string Prefix,
    string Hint,
    DateTime CreatedAt,
    string PlainKey,
    string Scopes);

/// <summary>
/// Obsolete LHDN-local command. Prefer <see cref="IApiCredentialService"/> (One platform credentials).
/// </summary>
[Obsolete("Platform credentials live in One. Use IApiCredentialService.GenerateAsync instead.")]
public record GenerateApiKeyCommand(Guid OrganizationId, string Name, bool IsTestMode) : ICommand<GenerateApiKeyResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

#pragma warning disable CS0618 // Obsolete façade intentionally retained for callers not yet migrated
public class GenerateApiKeyCommandHandler : ICommandHandler<GenerateApiKeyCommand, GenerateApiKeyResult>
{
    private readonly IApiCredentialService _credentials;

    public GenerateApiKeyCommandHandler(IApiCredentialService credentials)
    {
        _credentials = credentials;
    }

    public async Task<GenerateApiKeyResult> Handle(GenerateApiKeyCommand request, CancellationToken ct)
    {
        var created = await _credentials.GenerateAsync(
            request.OrganizationId,
            request.Name,
            request.IsTestMode,
            createdByUserId: null,
            scopes: null,
            ct);

        return new GenerateApiKeyResult(
            created.Id,
            created.Name,
            created.Prefix,
            created.Hint,
            created.CreatedAt,
            created.PlainKey,
            created.Scopes);
    }
}
#pragma warning restore CS0618
