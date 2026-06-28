using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Vault.Application.Commands;

public record UpdateVaultAssetCommand(
    Guid OrganizationId,
    Guid AssetId,
    List<Guid> ProductIds,
    string Name,
    string CloudflareR2Url) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateVaultAssetCommandHandler : ICommandHandler<UpdateVaultAssetCommand>
{
    private readonly IVaultRepository _repository;

    public UpdateVaultAssetCommandHandler(IVaultRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateVaultAssetCommand request, CancellationToken ct)
    {
        var asset = await _repository.GetByIdAsync(request.OrganizationId, request.AssetId, ct);

        if (asset == null)
        {
            throw new InvalidOperationException("Vault asset not found.");
        }

        asset.UpdateDetails(request.Name, request.CloudflareR2Url, request.ProductIds);

        await _repository.SaveChangesAsync(ct);
    }
}
