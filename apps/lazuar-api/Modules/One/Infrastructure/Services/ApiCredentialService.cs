using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Contracts;

namespace Modules.One.Infrastructure.Services;

public class ApiCredentialService : IApiCredentialService
{
    private readonly IMediator _mediator;
    private readonly IOneRepository _repository;

    public ApiCredentialService(IMediator mediator, IOneRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    public async Task<ApiCredentialGenerateResult> GenerateAsync(
        Guid organizationId,
        string name,
        bool isTestMode,
        Guid? createdByUserId = null,
        IReadOnlyList<string>? scopes = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GenerateApiCredentialCommand(organizationId, name, isTestMode, createdByUserId, scopes),
            ct);

        return new ApiCredentialGenerateResult(
            result.Id,
            result.Name,
            result.Prefix,
            result.Hint,
            result.CreatedAt,
            result.PlainKey,
            result.Scopes);
    }

    public async Task<IReadOnlyList<ApiCredentialSnapshot>> ListAsync(
        Guid organizationId,
        CancellationToken ct = default)
    {
        var keys = await _repository.ListApiCredentialsAsync(organizationId, ct);

        return keys.Select(k => new ApiCredentialSnapshot(
            k.Id,
            k.Name,
            k.Prefix,
            k.KeyHint,
            k.IsActive,
            k.CreatedAt,
            k.Scopes)).ToList();
    }

    public Task RevokeAsync(
        Guid organizationId,
        Guid credentialId,
        CancellationToken ct = default)
    {
        return _mediator.Send(new RevokeApiCredentialCommand(organizationId, credentialId), ct);
    }
}
