using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Application.Commands;

public class RestoreProductCommandHandler : ICommandHandler<RestoreProductCommand>
{
    private readonly ICommerceRepository _repository;

    public RestoreProductCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(RestoreProductCommand request, CancellationToken ct)
    {
        var product = await _repository.GetProductByIdAsync(request.ProductId, ct);

        if (product == null || product.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Product not found.");
        }

        product.Restore();

        await _repository.SaveChangesAsync(ct);
    }
}
