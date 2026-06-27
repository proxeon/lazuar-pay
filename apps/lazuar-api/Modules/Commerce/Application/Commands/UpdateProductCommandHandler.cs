using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.ValueObjects;

namespace Modules.Commerce.Application.Commands;

public class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand>
{
    private readonly ICommerceRepository _repository;

    public UpdateProductCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _repository.GetProductByIdAsync(request.ProductId, ct);

        if (product == null || product.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Product not found.");
        }

        var config = new CheckoutConfiguration(request.RequiresAddress, request.RequiresTaxId, request.RequiresPhone);

        product.UpdateDetails(
            request.Name,
            request.Slug,
            request.Price,
            request.PricingModel,
            request.MinimumPrice,
            request.Interval,
            request.IsActive,
            config,
            request.FulfillmentTargets
        );

        await _repository.SaveChangesAsync(ct);
    }
}
