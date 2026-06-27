using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;

namespace Modules.Commerce.Application.Commands;

public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly ICommerceRepository _repository;

    public CreateProductCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var config = new CheckoutConfiguration(request.RequiresAddress, request.RequiresTaxId, request.RequiresPhone);
        
        var product = new Product(
            request.OrganizationId,
            request.Name,
            request.Slug,
            request.Price,
            request.Currency,
            request.Interval,
            config,
            request.FulfillmentTargets
        );

        _repository.AddProduct(product);
        await _repository.SaveChangesAsync(ct);

        return product.Id;
    }
}
