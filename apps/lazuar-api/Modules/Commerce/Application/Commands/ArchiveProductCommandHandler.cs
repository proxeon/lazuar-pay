using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Application.Commands;

public class ArchiveProductCommandHandler : ICommandHandler<ArchiveProductCommand>
{
    private readonly ICommerceRepository _repository;

    public ArchiveProductCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ArchiveProductCommand request, CancellationToken ct)
    {
        var product = await _repository.GetProductByIdAsync(request.OrganizationId, request.ProductId, ct);

        if (product == null || product.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Product not found.");
        }

        product.Archive();

        await _repository.SaveChangesAsync(ct);
    }
}
