using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record InitiateCheckoutCommand(
    string TenantSlug,
    string ProductSlug,
    string Name,
    string Email,
    string? Phone,
    string? TaxId,
    string? CompanyName,
    string? AddressLine1,
    string? City,
    string? PostalCode,
    string? StateCode,
    string? CountryCode,
    int Quantity,
    bool IsGuestCheckout,
    string? CouponCode
) : ICommand<CheckoutResultDto>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record CheckoutResultDto(
    string Url,
    bool IsZeroAmountBypass
);
