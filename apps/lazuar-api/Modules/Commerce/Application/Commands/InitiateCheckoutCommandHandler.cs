using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Queries;

namespace Modules.Commerce.Application.Commands;

public class InitiateCheckoutCommandHandler : ICommandHandler<InitiateCheckoutCommand, CheckoutResultDto>
{
    private readonly IOneQueryService _oneQueryService;
    private readonly ICommerceRepository _repository;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public InitiateCheckoutCommandHandler(
        IOneQueryService oneQueryService,
        ICommerceRepository repository,
        IMediator mediator,
        IConfiguration configuration)
    {
        _oneQueryService = oneQueryService;
        _repository = repository;
        _mediator = mediator;
        _configuration = configuration;
    }

    public async Task<CheckoutResultDto> Handle(InitiateCheckoutCommand request, CancellationToken ct)
    {
        var tenantId = await _oneQueryService.GetTenantIdBySlugAsync(request.TenantSlug);
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException($"Workspace with slug '{request.TenantSlug}' not found.");
        }

        var clientUrl = _configuration["App:ClientUrl"]?.TrimEnd('/') ?? "http://localhost:3004";

        if (request.SessionId.HasValue)
        {
            var existingSession = await _repository.GetCheckoutSessionByIdAsync(request.SessionId.Value, ct);
            if (existingSession == null || existingSession.OrganizationId != tenantId.Value || existingSession.Status != "OPEN")
            {
                throw new InvalidOperationException("Invalid or completed custom checkout session.");
            }

            decimal customTotalAmount = existingSession.AdHocLineItems.Sum(x => x.UnitPrice * x.Quantity);
            
            var customSuccessUrl = $"{clientUrl}/{request.TenantSlug}/checkout/custom/success?sub_id={existingSession.Id}";
            var customCancelUrl = $"{clientUrl}/{request.TenantSlug}/pay/{existingSession.Id}?cancelled=true";

            var customMetadata = new Dictionary<string, string>
            {
                { "type", "custom_payment_link" },
                { "subscription_id", existingSession.Id.ToString() },
                { "tenant_id", tenantId.Value.ToString() }
            };

            var customGatewayQuery = new GenerateCheckoutSessionQuery(
                tenantId.Value,
                customTotalAmount,
                "MYR",
                "Custom Payment Request",
                request.Email,
                customSuccessUrl,
                customCancelUrl,
                customMetadata,
                false,
                1
            );

            var customCheckoutUrl = await _mediator.Send(customGatewayQuery, ct);
            return new CheckoutResultDto(customCheckoutUrl, false);
        }

        var product = await _repository.GetProductBySlugAsync(tenantId.Value, request.ProductSlug, ct);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with slug '{request.ProductSlug}' not found or is inactive.");
        }

        BillingAddressDto? billingAddress = null;
        if (!string.IsNullOrEmpty(request.AddressLine1))
        {
            billingAddress = new BillingAddressDto
            {
                Line1 = request.AddressLine1,
                City = request.City ?? "",
                Postal_code = request.PostalCode ?? "",
                State_code = request.StateCode ?? "",
                Country_code = request.CountryCode ?? "MYS"
            };
        }

        var resolveCrmProfileCmd = new ResolveClientProfileCommand(
            tenantId.Value,
            request.Name,
            request.Email,
            request.Phone ?? "",
            request.TaxId,
            null,
            request.CompanyName,
            billingAddress
        );

        var clientProfileId = await _mediator.Send(resolveCrmProfileCmd, ct);

        decimal basePrice = product.Price * request.Quantity;
        decimal discountAmount = 0m;
        Guid? couponId = null;

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _repository.GetCouponByCodeWithLockAsync(tenantId.Value, request.CouponCode, ct);
            if (coupon == null)
            {
                throw new InvalidOperationException($"Coupon with code '{request.CouponCode}' is invalid or expired.");
            }

            coupon.Validate(product.Price, product.Id);
            discountAmount = coupon.CalculateDiscount(product.Price) * request.Quantity;
            coupon.Reserve();
            couponId = coupon.Id;
        }

        decimal netAmount = Math.Max(0, basePrice - discountAmount);

        var session = new CheckoutSession(
            tenantId.Value,
            clientProfileId,
            product.Id,
            couponId,
            DateTime.UtcNow.AddHours(24)
        );

        _repository.AddCheckoutSession(session);
        await _repository.SaveChangesAsync(ct);

        if (netAmount == 0)
        {
            var processZeroAmountCmd = new ProcessZeroAmountCheckoutCommand(tenantId.Value, session.Id);
            await _mediator.Send(processZeroAmountCmd, ct);

            return new CheckoutResultDto(string.Empty, true);
        }
        else
        {
            var successUrl = !string.IsNullOrWhiteSpace(product.SuccessUrl) 
                ? $"{product.SuccessUrl}?sub_id={session.Id}" 
                : $"{clientUrl}/{request.TenantSlug}/checkout/{request.ProductSlug}/success?sub_id={session.Id}";
                
            var cancelUrl = !string.IsNullOrWhiteSpace(product.CancelUrl) 
                ? product.CancelUrl 
                : $"{clientUrl}/{request.TenantSlug}/checkout/{request.ProductSlug}?cancelled=true";

            var metadata = new Dictionary<string, string>
            {
                { "type", "commerce_subscription" },
                { "subscription_id", session.Id.ToString() },
                { "tenant_id", tenantId.Value.ToString() }
            };

            var gatewayQuery = new GenerateCheckoutSessionQuery(
                tenantId.Value,
                netAmount,
                product.Currency,
                product.Name,
                request.Email,
                successUrl,
                cancelUrl,
                metadata,
                product.Interval != "one_time",
                request.Quantity
            );

            var checkoutUrl = await _mediator.Send(gatewayQuery, ct);

            return new CheckoutResultDto(checkoutUrl, false);
        }
    }
}
