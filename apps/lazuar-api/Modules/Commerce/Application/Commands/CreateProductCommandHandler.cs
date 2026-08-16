using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Communications.Contracts;

namespace Modules.Commerce.Application.Commands;

public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly ICommerceRepository _repository;
    private readonly ICommunicationsQueryService _communicationsQueryService;

    public CreateProductCommandHandler(
        ICommerceRepository repository,
        ICommunicationsQueryService communicationsQueryService)
    {
        _repository = repository;
        _communicationsQueryService = communicationsQueryService;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var config = new CheckoutConfiguration(request.RequiresAddress, request.RequiresTaxId, request.RequiresPhone);
        
        var product = new Product(
            request.OrganizationId,
            request.Name,
            request.Slug,
            request.Price,
            request.PricingModel,
            request.MinimumPrice,
            request.Currency,
            request.Interval,
            request.GatewayName,
            config,
            request.FulfillmentTargets
        );

        var hasEmailConfig = await _communicationsQueryService.HasValidEmailConfigAsync(request.OrganizationId);
        if (!hasEmailConfig)
        {
            product.Archive();
        }

        product.SetSst(request.SstTaxType, request.SstRatePercent);
        _repository.AddProduct(product);
        await _repository.SaveChangesAsync(ct);

        return product.Id;
    }
}
