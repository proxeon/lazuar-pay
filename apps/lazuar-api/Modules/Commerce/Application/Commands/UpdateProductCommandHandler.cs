using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Communications.Application.Queries;

namespace Modules.Commerce.Application.Commands;

public class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly ICommunicationsQueryService _communicationsQueryService;

    public UpdateProductCommandHandler(
        ICommerceRepository repository,
        ICommunicationsQueryService communicationsQueryService)
    {
        _repository = repository;
        _communicationsQueryService = communicationsQueryService;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _repository.GetProductByIdAsync(request.ProductId, ct);

        if (product == null || product.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Product not found.");
        }

        if (request.IsActive)
        {
            var hasEmailConfig = await _communicationsQueryService.HasValidEmailConfigAsync(request.OrganizationId);
            if (!hasEmailConfig)
            {
                throw new BusinessRuleValidationException(new GenericBusinessRule("You must configure a valid Resend API key before activating checkout links."));
            }
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
            request.GatewayName,
            config,
            request.FulfillmentTargets
        );

        await _repository.SaveChangesAsync(ct);
    }
}
