using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Contracts.Events;

namespace Modules.One.Application.Commands;

public record RevokeApiCredentialCommand(Guid OrganizationId, Guid CredentialId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RevokeApiCredentialCommandHandler : ICommandHandler<RevokeApiCredentialCommand>
{
    private readonly IOneRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly IAuditRecorder? _auditRecorder;
    private readonly IApiKeyAuthCache? _authCache;

    public RevokeApiCredentialCommandHandler(
        IOneRepository repository,
        [FromKeyedServices("OneEventBus")] IEventBus eventBus,
        IAuditRecorder? auditRecorder = null,
        IApiKeyAuthCache? authCache = null)
    {
        _repository = repository;
        _eventBus = eventBus;
        _auditRecorder = auditRecorder;
        _authCache = authCache;
    }

    public async Task Handle(RevokeApiCredentialCommand request, CancellationToken ct)
    {
        var credential = await _repository.GetApiCredentialAsync(request.CredentialId, ct);

        if (credential == null || credential.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("API credential not found or does not belong to this organization.");
        }

        if (!credential.IsActive)
        {
            return; // Already revoked
        }

        credential.Revoke();
        _authCache?.Evict(credential.KeyHash);

        await _eventBus.PublishAsync(new ApiKeyRevokedIntegrationEvent(credential.OrganizationId, credential.KeyHash));
        await _repository.SaveChangesAsync(ct);

        if (_auditRecorder != null)
        {
            await _auditRecorder.RecordAsync(
                request.OrganizationId,
                "api_key.revoked",
                "api_credential",
                credential.Id.ToString(),
                new { name = credential.Name, hint = credential.KeyHint },
                ct: ct);
        }
    }
}
