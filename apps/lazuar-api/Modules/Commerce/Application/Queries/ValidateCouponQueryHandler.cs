using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Commerce.Application.Commands;

namespace Modules.Commerce.Application.Queries;

public class ValidateCouponQueryHandler : IQueryHandler<ValidateCouponQuery, ValidateCouponResponseDto>
{
    private readonly ICommerceRepository _repository;

    public ValidateCouponQueryHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidateCouponResponseDto> Handle(ValidateCouponQuery request, CancellationToken ct)
    {
        var product = await _repository.GetProductBySlugAsync(request.TenantId, request.ProductSlug, ct);
        if (product == null)
        {
            throw new InvalidOperationException("Product not found.");
        }

        var coupon = await _repository.GetCouponByCodeAsync(request.TenantId, request.CouponCode, ct);
        if (coupon == null)
        {
            throw new InvalidOperationException("Invalid promo code.");
        }

        var quantity = Math.Clamp(request.Quantity, 1, 99);
        var resolved = InitiateCheckoutCommandHandler.ResolveCheckoutPrice(
            product, request.PriceId, request.Interval);

        coupon.Validate(resolved.Amount, product.Id);

        var unitDiscount = coupon.CalculateDiscount(resolved.Amount);
        var lineDiscount = unitDiscount * quantity;
        var finalPrice = Math.Max(0, resolved.Amount - unitDiscount) * quantity;

        return new ValidateCouponResponseDto
        {
            Is_valid = true,
            Discount_amount = (double)lineDiscount,
            Final_price = (double)finalPrice
        };
    }
}
