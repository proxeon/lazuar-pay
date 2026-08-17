using System;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Application.Queries;

public record ValidateCouponQuery(
    Guid TenantId,
    string ProductSlug,
    string CouponCode,
    Guid? PriceId = null,
    string? Interval = null,
    int Quantity = 1) : IQuery<ValidateCouponResponseDto>;
