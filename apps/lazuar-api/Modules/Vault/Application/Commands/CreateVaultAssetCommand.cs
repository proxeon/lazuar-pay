using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Vault.Domain.Aggregates;

namespace Modules.Vault.Application.Commands;

public record CreateVaultAssetCommand(
    Guid OrganizationId,
    Guid ProductId,
    string Name,
    string CloudflareR2Url) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateVaultAssetCommandHandler : ICommandHandler<CreateVaultAssetCommand, Guid>
{
    private readonly IVaultRepository _repository;

    public CreateVaultAssetCommandHandler(IVaultRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateVaultAssetCommand request, CancellationToken ct)
    {
        var asset = new VaultAsset(
            request.OrganizationId,
            request.ProductId,
            request.Name,
            request.CloudflareR2Url
        );

        _repository.Add(asset);
        await _repository.SaveChangesAsync(ct);

        return asset.Id;
    }
}
