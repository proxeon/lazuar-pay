using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Vault.Application.Commands;

public record DeleteVaultAssetCommand(Guid OrganizationId, Guid AssetId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class DeleteVaultAssetCommandHandler : ICommandHandler<DeleteVaultAssetCommand>
{
    private readonly IVaultRepository _repository;

    public DeleteVaultAssetCommandHandler(IVaultRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteVaultAssetCommand request, CancellationToken ct)
    {
        var asset = await _repository.GetByIdAsync(request.OrganizationId, request.AssetId, ct);

        if (asset == null)
        {
            throw new InvalidOperationException("Vault asset not found.");
        }

        _repository.Remove(asset);

        await _repository.SaveChangesAsync(ct);
    }
}
