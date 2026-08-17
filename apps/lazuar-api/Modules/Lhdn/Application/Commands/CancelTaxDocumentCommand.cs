using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Lhdn.Application.Commands;

public record CancelTaxDocumentCommand(Guid OrganizationId, string InternalId, string Reason) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CancelTaxDocumentCommandHandler : ICommandHandler<CancelTaxDocumentCommand>
{
    private readonly ILhdnRepository _repository;
    private readonly ILhdnGatewayAdapter _gatewayAdapter;
    private readonly IEventBus _eventBus;
    private readonly ISecretVault _secretVault;

    public CancelTaxDocumentCommandHandler(
        ILhdnRepository repository,
        ILhdnGatewayAdapter gatewayAdapter,
        [FromKeyedServices("LhdnEventBus")] IEventBus eventBus,
        ISecretVault secretVault)
    {
        _repository = repository;
        _gatewayAdapter = gatewayAdapter;
        _eventBus = eventBus;
        _secretVault = secretVault;
    }

    public async Task Handle(CancelTaxDocumentCommand request, CancellationToken ct)
    {
        var doc = await _repository.GetTaxDocumentByInternalIdAsync(request.OrganizationId, request.InternalId, ct);
        if (doc == null || string.IsNullOrEmpty(doc.LhdnUuid))
        {
            throw new InvalidOperationException("Document not found or has not been assigned a MyInvois UUID.");
        }

        var config = await _repository.GetTenantConfigAsync(request.OrganizationId, ct);
        if (config == null || string.IsNullOrWhiteSpace(config.MyInvoisClientId) || string.IsNullOrWhiteSpace(config.MyInvoisClientSecret))
        {
            throw new InvalidOperationException("Tenant configuration or API credentials missing.");
        }

        // Apply domain rule enforcement in-memory (e.g., the 72-hour window limit).
        doc.Cancel();

        var clientSecret = _secretVault.DecryptOrPlaintext(config.MyInvoisClientSecret);
        var token = await _gatewayAdapter.GetTokenAsync(config.OrganizationId, config.MyInvoisClientId, clientSecret, config.IntermediaryMode, config.SupplierTin, ct, config.Environment);
        var result = await _gatewayAdapter.CancelDocumentAsync(config.MyInvoisClientId, token, doc.LhdnUuid, request.Reason, config.IntermediaryMode, config.SupplierTin, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException($"LHDN Cancellation failed: {result.ErrorMessage}");
        }

        await _eventBus.PublishAsync(new LhdnDocumentCancelledIntegrationEvent(request.OrganizationId, request.InternalId, doc.LhdnUuid, request.Reason));
        await _repository.SaveChangesAsync(ct);
    }
}
