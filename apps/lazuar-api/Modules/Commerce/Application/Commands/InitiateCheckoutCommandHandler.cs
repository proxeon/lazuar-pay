using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Queries;
using Modules.Communications.Contracts;
using Modules.Billing.Contracts;

namespace Modules.Commerce.Application.Commands;

public class InitiateCheckoutCommandHandler : ICommandHandler<InitiateCheckoutCommand, CheckoutResultDto>
{
    private readonly IOneQueryService _oneQueryService;
    private readonly ICommerceRepository _repository;
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ICommunicationsQueryService _communicationsQueryService;
    private readonly IBillingQueryService? _billingQueryService;

    public InitiateCheckoutCommandHandler(
        IOneQueryService oneQueryService,
        ICommerceRepository repository,
        IMediator mediator,
        IConfiguration configuration,
        ICommunicationsQueryService communicationsQueryService,
        IBillingQueryService? billingQueryService = null)
    {
        _oneQueryService = oneQueryService;
        _repository = repository;
        _mediator = mediator;
        _configuration = configuration;
        _communicationsQueryService = communicationsQueryService;
        _billingQueryService = billingQueryService;
    }

    public async Task<CheckoutResultDto> Handle(InitiateCheckoutCommand request, CancellationToken ct)
    {
        var tenantId = await _oneQueryService.GetTenantIdBySlugAsync(request.TenantSlug);
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException($"Workspace with slug '{request.TenantSlug}' not found.");
        }

        var hasEmailConfig = await _communicationsQueryService.HasValidEmailConfigAsync(tenantId.Value);
        if (!hasEmailConfig)
        {
            throw new InvalidOperationException("This workspace has not configured an active email provider. Checkout is temporarily disabled.");
        }

        var idempotencyKey = CommerceCheckoutIdempotency.NormalizeKey(request.IdempotencyKey);
        var fingerprint = CommerceCheckoutIdempotency.Fingerprint(
            tenantId.Value,
            request.ProductSlug,
            request.Email,
            request.CouponCode,
            request.Quantity,
            request.SessionId,
            request.Interval,
            request.PriceId);

        CheckoutSession? reuseSession = null;
        if (idempotencyKey != null)
        {
            var existing = await _repository.GetCheckoutSessionByIdempotencyKeyAsync(
                tenantId.Value, idempotencyKey, ct);
            if (existing != null)
            {
                if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("IDEMPOTENCY_CONFLICT: Idempotency-Key was reused with a different checkout payload.");
                }

                var now = DateTime.UtcNow;
                if (CommerceCheckoutIdempotency.TryReplayUrl(existing, now, out var replayUrl))
                {
                    return new CheckoutResultDto(replayUrl!, existing.Status == "COMPLETED");
                }

                if (CommerceCheckoutIdempotency.IsReplayableOpen(existing, now)
                    && string.IsNullOrWhiteSpace(existing.GatewayCheckoutUrl))
                {
                    reuseSession = existing;
                }
                else if (CommerceCheckoutIdempotency.ShouldReleaseKey(existing, now))
                {
                    existing.ClearIdempotency();
                    await _repository.SaveChangesAsync(ct);
                }
            }
        }

        var clientUrl = _configuration["App:ClientUrl"]?.TrimEnd('/') ?? "http://localhost:3004";

        if (request.SessionId.HasValue)
        {
            var existingSession = await _repository.GetCheckoutSessionByIdAsync(request.SessionId.Value, ct);
            if (existingSession == null || existingSession.OrganizationId != tenantId.Value || existingSession.Status != "OPEN")
            {
                throw new InvalidOperationException("Invalid or completed custom checkout session.");
            }

            if (CommerceCheckoutIdempotency.TryReplayUrl(existingSession, DateTime.UtcNow, out var existingQuoteUrl))
            {
                return new CheckoutResultDto(existingQuoteUrl!, false);
            }

            existingSession.SetIdempotency(idempotencyKey, fingerprint);

            var customNet = existingSession.AdHocLineItems.Sum(x => x.UnitPrice * x.Quantity);
            var customMerchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
                _billingQueryService, tenantId.Value);
            var customBreakdown = SubscriptionBillingAmount.CustomQuoteBreakdown(
                customNet, customMerchantHasSst);
            var customTotalAmount = customBreakdown.Gross;
            
            var customSuccessUrl = $"{clientUrl}/{request.TenantSlug}/checkout/custom/success?sub_id={existingSession.Id}";
            var customCancelUrl = $"{clientUrl}/{request.TenantSlug}/pay/{existingSession.Id}?cancelled=true";

            var customMetadata = new Dictionary<string, string>
            {
                { "type", "custom_payment_link" },
                { "subscription_id", existingSession.Id.ToString() },
                { "tenant_id", tenantId.Value.ToString() },
                { "is_b2b_required", existingSession.IsB2bRequired ? "true" : "false" }
            };
            SubscriptionBillingAmount.StampSstMetadata(customMetadata, customBreakdown);
            if (customBreakdown.UnitTax > 0)
            {
                customMetadata["sst_rate_percent"] =
                    SubscriptionBillingAmount.DefaultServiceTaxRatePercent.ToString("0.##");
            }

            if (existingSession.IsB2bRequired)
            {
                if (string.IsNullOrWhiteSpace(request.TaxId))
                {
                    throw new InvalidOperationException("This payment request requires a tax ID.");
                }

                if (string.IsNullOrWhiteSpace(request.CompanyName))
                {
                    throw new InvalidOperationException("This payment request requires a company name.");
                }

                if (string.IsNullOrWhiteSpace(request.IdType) || string.IsNullOrWhiteSpace(request.IdValue))
                {
                    throw new InvalidOperationException("This payment request requires buyer ID type and ID value (BRN / NRIC / PASSPORT / ARMY).");
                }

                BillingAddressDto? customBillingAddress = null;
                if (!string.IsNullOrEmpty(request.AddressLine1))
                {
                    customBillingAddress = new BillingAddressDto
                    {
                        Line1 = request.AddressLine1,
                        City = request.City ?? "",
                        Postal_code = request.PostalCode ?? "",
                        State_code = request.StateCode ?? "",
                        Country_code = Iso3166Country.NormalizeToAlpha3(request.CountryCode)
                    };
                }

                await _mediator.Send(new ResolveClientProfileCommand(
                    tenantId.Value,
                    request.Name,
                    request.Email,
                    request.Phone ?? "",
                    Tin: request.TaxId,
                    IdType: request.IdType,
                    IdValue: request.IdValue,
                    BillingAddress: customBillingAddress,
                    CompanyName: request.CompanyName), ct);
            }

            // Prefer gateway stored on the custom session; Payments falls back to first active → BILLPLZ.
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
                1,
                existingSession.GatewayName
            );

            var customCheckoutUrl = await _mediator.Send(customGatewayQuery, ct);
            existingSession.SetGatewayCheckoutUrl(customCheckoutUrl);
            await _repository.SaveChangesAsync(ct);
            return new CheckoutResultDto(customCheckoutUrl, false);
        }

        var product = await _repository.GetProductBySlugAsync(tenantId.Value, request.ProductSlug, ct);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with slug '{request.ProductSlug}' not found or is inactive.");
        }

        var quantity = CommerceCheckoutQuantity.NormalizeOrThrow(request.Quantity, product);
        var resolved = ResolveCheckoutPrice(product, request.PriceId, request.Interval);

        if (product.TrialDays > 0 && string.Equals(resolved.Interval, "one_time", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Free trial is not available on one-time products.");
        }

        var isTrial = SubscriptionActivation.IsTrialOffer(product)
            && resolved.Interval is "mo" or "yr";

        EnforceCheckoutConfiguration(product, request);

        BillingAddressDto? billingAddress = null;
        if (!string.IsNullOrEmpty(request.AddressLine1))
        {
            billingAddress = new BillingAddressDto
            {
                Line1 = request.AddressLine1,
                City = request.City ?? "",
                Postal_code = request.PostalCode ?? "",
                State_code = request.StateCode ?? "",
                Country_code = Iso3166Country.NormalizeToAlpha3(request.CountryCode)
            };
        }

        var resolveCrmProfileCmd = new ResolveClientProfileCommand(
            tenantId.Value,
            request.Name,
            request.Email,
            request.Phone ?? "",
            Tin: request.TaxId,
            IdType: request.IdType,
            IdValue: request.IdValue,
            BillingAddress: billingAddress,
            CompanyName: request.CompanyName
        );

        var clientProfileId = await _mediator.Send(resolveCrmProfileCmd, ct);

        decimal unitDiscount = 0m;
        Guid? couponId = null;
        CheckoutSession? session = reuseSession;

        async Task PersistReservationAndSessionAsync(CancellationToken persistCt)
        {
            if (!isTrial && !string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var coupon = await _repository.GetCouponByCodeWithLockAsync(tenantId.Value, request.CouponCode, persistCt);
                if (coupon == null)
                {
                    throw new InvalidOperationException($"Coupon with code '{request.CouponCode}' is invalid or expired.");
                }

                coupon.Validate(resolved.Amount, product.Id);
                unitDiscount = coupon.CalculateDiscount(resolved.Amount);
                coupon.Reserve();
                couponId = coupon.Id;
            }

            session = new CheckoutSession(
                tenantId.Value,
                clientProfileId,
                product.Id,
                couponId,
                DateTime.UtcNow.AddHours(24),
                quantity,
                resolved.PriceId
            );

            var persistMeta = CommerceCheckoutMetadata.ForPersistence(request.Metadata, resolved.Interval);
            if (!string.IsNullOrWhiteSpace(request.TaxId))
            {
                persistMeta["is_b2b_required"] = "true";
            }
            session.SetMetadataJson(CommerceCheckoutMetadata.Serialize(persistMeta));
            session.SetIdempotency(idempotencyKey, fingerprint);

            _repository.AddCheckoutSession(session);
            await _repository.SaveChangesAsync(persistCt);
        }

        try
        {
            if (session == null)
            {
                if (_repository is ICommerceTransactional transactional)
                {
                    await transactional.ExecuteInTransactionAsync(PersistReservationAndSessionAsync, ct);
                }
                else
                {
                    await PersistReservationAndSessionAsync(ct);
                }
            }
        }
        catch (Exception) when (idempotencyKey != null)
        {
            var raced = await _repository.GetCheckoutSessionByIdempotencyKeyAsync(
                tenantId.Value, idempotencyKey, ct);
            if (raced != null && (session == null || raced.Id != session.Id))
            {
                if (!string.Equals(raced.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("IDEMPOTENCY_CONFLICT: Idempotency-Key was reused with a different checkout payload.");
                }

                if (!string.IsNullOrWhiteSpace(raced.GatewayCheckoutUrl))
                {
                    return new CheckoutResultDto(raced.GatewayCheckoutUrl, raced.Status == "COMPLETED");
                }

                session = raced;
            }
            else
            {
                throw;
            }
        }

        if (session == null)
        {
            throw new InvalidOperationException("Checkout session was not created.");
        }

        var unitNet = isTrial ? 0m : Math.Max(0, resolved.Amount - unitDiscount);
        var merchantHasSst = await SubscriptionBillingAmount.MerchantHasSstAsync(
            _billingQueryService, tenantId.Value);
        var breakdown = SubscriptionBillingAmount.GrossBreakdown(
            unitNet, quantity, product.SstTaxType, product.SstRatePercent, merchantHasSst);
        var sstType = breakdown.TaxType;
        var unitTax = breakdown.UnitTax;
        var unitGross = breakdown.UnitGross;
        var lineNet = breakdown.Gross;
        var isB2bRequired = !string.IsNullOrWhiteSpace(request.TaxId);

        // Same poller handle as the paid hop-2 return — buyer success must observe session COMPLETED.
        var successUrl = $"{clientUrl}/{request.TenantSlug}/checkout/{request.ProductSlug}/success?sub_id={session.Id}";

        if (lineNet == 0)
        {
            var vaultingRecurring = PaymentGatewayCapabilities.SupportsOffSession(product.GatewayName)
                && resolved.Interval is "mo" or "yr";
            if (vaultingRecurring)
            {
                var cancelUrl = $"{clientUrl}/{request.TenantSlug}/checkout/{request.ProductSlug}?cancelled=true";
                var vaultMetadata = CommerceCheckoutMetadata.MergeClientIntoGateway(
                    request.Metadata,
                    tenantId.Value,
                    session.Id,
                    isB2bRequired);
                vaultMetadata["client_profile_id"] = clientProfileId.ToString();
                var vaultQuery = new GenerateCheckoutSessionQuery(
                    tenantId.Value,
                    0m,
                    product.Currency,
                    product.Name,
                    request.Email,
                    successUrl,
                    cancelUrl,
                    vaultMetadata,
                    true,
                    quantity,
                    string.IsNullOrWhiteSpace(product.GatewayName) ? null : product.GatewayName
                );
                var vaultUrl = await _mediator.Send(vaultQuery, ct);
                session.SetGatewayCheckoutUrl(vaultUrl);
                await _repository.SaveChangesAsync(ct);
                return new CheckoutResultDto(vaultUrl, false);
            }

            var processZeroAmountCmd = new ProcessZeroAmountCheckoutCommand(tenantId.Value, session.Id);
            await _mediator.Send(processZeroAmountCmd, ct);
            session.SetGatewayCheckoutUrl(successUrl);
            await _repository.SaveChangesAsync(ct);

            return new CheckoutResultDto(successUrl, true);
        }
        else
        {
            var cancelUrl = $"{clientUrl}/{request.TenantSlug}/checkout/{request.ProductSlug}?cancelled=true";

            var metadata = CommerceCheckoutMetadata.MergeClientIntoGateway(
                request.Metadata,
                tenantId.Value,
                session.Id,
                isB2bRequired);
            metadata["client_profile_id"] = clientProfileId.ToString();
            if (unitTax > 0)
            {
                metadata["sst_tax_type"] = sstType;
                metadata["sst_tax_amount"] = (unitTax * quantity).ToString("0.00");
                metadata["sst_rate_percent"] = product.SstRatePercent.ToString("0.##");
            }

            // Product gateway when set; Payments resolves first active → BILLPLZ if blank (legacy rows).
            var preferredGateway = string.IsNullOrWhiteSpace(product.GatewayName)
                ? null
                : product.GatewayName;

            // Amount is unit price (net + SST); adapters multiply by Quantity. Do not pre-multiply.
            var gatewayQuery = new GenerateCheckoutSessionQuery(
                tenantId.Value,
                unitGross,
                product.Currency,
                product.Name,
                request.Email,
                successUrl,
                cancelUrl,
                metadata,
                resolved.Interval != "one_time",
                quantity,
                preferredGateway
            );

            var checkoutUrl = await _mediator.Send(gatewayQuery, ct);
            session.SetGatewayCheckoutUrl(checkoutUrl);
            await _repository.SaveChangesAsync(ct);

            return new CheckoutResultDto(checkoutUrl, false);
        }
    }

    internal static (decimal Amount, string Interval, Guid? PriceId) ResolveCheckoutPrice(
        Domain.Aggregates.Product product,
        Guid? priceId,
        string? interval)
    {
        if (priceId.HasValue)
        {
            var byId = product.Prices.FirstOrDefault(p => p.Id == priceId.Value);
            if (byId == null)
            {
                throw new InvalidOperationException("price_id is not valid for this product.");
            }

            return (byId.Amount, byId.Interval, byId.Id);
        }

        if (!string.IsNullOrWhiteSpace(interval))
        {
            var normalized = interval.Trim().ToLowerInvariant();
            var byInterval = product.GetPrice(normalized);
            if (byInterval == null)
            {
                if (string.Equals(product.Interval, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return (product.Price, product.Interval, product.DefaultPrice()?.Id);
                }

                throw new InvalidOperationException($"This product has no {normalized} price.");
            }

            return (byInterval.Amount, byInterval.Interval, byInterval.Id);
        }

        return (product.Price, product.Interval, product.DefaultPrice()?.Id);
    }

    private static void EnforceCheckoutConfiguration(Domain.Aggregates.Product product, InitiateCheckoutCommand request)
    {
        var config = product.CheckoutConfiguration;
        if (config == null)
        {
            return;
        }

        if (config.RequiresPhone && string.IsNullOrWhiteSpace(request.Phone))
        {
            throw new InvalidOperationException("This product requires a phone number at checkout.");
        }

        if (config.RequiresTaxId && string.IsNullOrWhiteSpace(request.TaxId))
        {
            throw new InvalidOperationException("This product requires a tax ID at checkout.");
        }

        if (config.RequiresTaxId && (string.IsNullOrWhiteSpace(request.IdType) || string.IsNullOrWhiteSpace(request.IdValue)))
        {
            throw new InvalidOperationException("This product requires buyer ID type and ID value (BRN / NRIC / PASSPORT / ARMY).");
        }

        if (config.RequiresTaxId && string.IsNullOrWhiteSpace(request.CompanyName))
        {
            throw new InvalidOperationException("This product requires a company name at checkout.");
        }

        if (config.RequiresAddress)
        {
            if (string.IsNullOrWhiteSpace(request.AddressLine1)
                || string.IsNullOrWhiteSpace(request.City)
                || string.IsNullOrWhiteSpace(request.PostalCode)
                || string.IsNullOrWhiteSpace(request.StateCode))
            {
                throw new InvalidOperationException("This product requires a complete billing address at checkout.");
            }
        }
    }
}
