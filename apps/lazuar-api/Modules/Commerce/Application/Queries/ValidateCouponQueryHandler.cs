using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

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

        coupon.Validate(product.Price, product.Id);

        var discount = coupon.CalculateDiscount(product.Price);
        var finalPrice = Math.Max(0, product.Price - discount);

        return new ValidateCouponResponseDto
        {
            Is_valid = true,
            Discount_amount = (double)discount,
            Final_price = (double)finalPrice
        };
    }
}
