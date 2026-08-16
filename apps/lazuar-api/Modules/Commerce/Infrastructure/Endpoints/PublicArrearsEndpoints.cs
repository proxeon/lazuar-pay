using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Queries;

namespace Modules.Commerce.Infrastructure;

public static class PublicArrearsEndpoints
{
    public static RouteGroupBuilder MapPublicArrearsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/checkout/{subId:guid}/arrears", async Task<Results<Ok<ArrearsSummaryDto>, NotFound>> (
            Guid subId,
            [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory sqlFactory) =>
        {
            using var connection = sqlFactory.CreateConnection();
            var query = @"
                SELECT p.""Name"" as ProductName,
                       (CASE WHEN s.""UnitAmount"" > 0 THEN s.""UnitAmount"" ELSE p.""Price"" END)
                           * GREATEST(s.""Quantity"", 1) as Amount,
                       p.""Currency"", s.""Status"",
                       p.""GatewayName"" as ProductGatewayName
                FROM commerce.""Subscriptions"" s
                JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
                WHERE s.""Id"" = @SubId LIMIT 1";

            var row = await Dapper.SqlMapper.QuerySingleOrDefaultAsync<ArrearsGetRow>(
                connection, query, new { SubId = subId });
            if (row == null) return TypedResults.NotFound();

            var dto = new ArrearsSummaryDto
            {
                Product_name = row.ProductName,
                Amount = (double)row.Amount,
                Currency = row.Currency,
                Status = row.Status,
                Is_reminder_only = PaymentGatewayCapabilities.IsReminderOnlyGateway(row.ProductGatewayName)
            };
            return TypedResults.Ok(dto);
        });

        group.MapPost("/checkout/{subId:guid}/update-payment", async Task<Results<Ok<CheckoutResponse>, BadRequest<string>>> (
            Guid subId,
            [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory sqlFactory,
            ICrmQueryService crmQueryService,
            IOneQueryService oneQueryService,
            IMediator mediator,
            IConfiguration config) =>
        {
            // L-03: commerce-owned SQL only; CRM email + One tenant slug via contracts ports.
            using var connection = sqlFactory.CreateConnection();
            var query = @"
                SELECT s.""OrganizationId"", s.""ProductId"", s.""ClientProfileId"", s.""Status"", s.""CurrentDunningCampaignId"",
                       s.""CurrentRenewalCheckoutUrl"", s.""CurrentRenewalCheckoutForDate"", s.""NextBillingDate"",
                       p.""Name"" as ProductName,
                       (CASE WHEN s.""UnitAmount"" > 0 THEN s.""UnitAmount"" ELSE p.""Price"" END)
                           * GREATEST(s.""Quantity"", 1) as Price,
                       p.""Currency"", p.""GatewayName"" as ProductGatewayName
                FROM commerce.""Subscriptions"" s
                JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
                WHERE s.""Id"" = @SubId LIMIT 1";

            var sub = await Dapper.SqlMapper.QuerySingleOrDefaultAsync<ArrearsUpdatePaymentRow>(
                connection, query, new { SubId = subId });

            if (sub == null) return TypedResults.BadRequest("Subscription not found.");
            if (sub.Status == "CANCELED")
            {
                return TypedResults.BadRequest("This subscription is canceled.");
            }

            var reminderOnly = PaymentGatewayCapabilities.IsReminderOnlyGateway(sub.ProductGatewayName);
            if (sub.Status == "ACTIVE" && reminderOnly)
            {
                return TypedResults.BadRequest("REMINDER_ONLY: This plan is paid by invoice each cycle. No card on file.");
            }

            if (sub.Status != "PAST_DUE" && sub.Status != "SUSPENDED" && sub.Status != "ACTIVE")
            {
                return TypedResults.BadRequest("This subscription cannot update a payment method.");
            }

            var cached = !string.IsNullOrWhiteSpace(sub.CurrentRenewalCheckoutUrl)
                && sub.CurrentRenewalCheckoutForDate.HasValue
                && (sub.Status == "ACTIVE"
                    ? sub.CurrentRenewalCheckoutForDate.Value.Date == DateTime.UtcNow.Date
                    : sub.NextBillingDate.HasValue
                      && sub.CurrentRenewalCheckoutForDate.Value.Date == sub.NextBillingDate.Value.Date);
            if (cached)
            {
                return TypedResults.Ok(new CheckoutResponse { Url = sub.CurrentRenewalCheckoutUrl!, Is_zero_amount_bypass = false });
            }

            // Former multi-schema JOIN semantics: missing profile/org → not found.
            var profile = await crmQueryService.GetClientProfileAsync(sub.ClientProfileId);
            if (profile == null) return TypedResults.BadRequest("Subscription not found.");

            var workspace = await oneQueryService.GetWorkspaceByIdAsync(sub.OrganizationId);
            if (workspace == null) return TypedResults.BadRequest("Subscription not found.");

            var clientUrl = config["App:ClientUrl"]?.TrimEnd('/') ?? "http://localhost:3004";
            var successUrl = $"{clientUrl}/{workspace.Slug}/portal";
            var cancelUrl = $"{clientUrl}/{workspace.Slug}/update-payment/{subId}";

            var isActiveUpdate = sub.Status == "ACTIVE";
            var chargeAmount = isActiveUpdate ? 1m : sub.Price;

            var metadata = new Dictionary<string, string>
            {
                { "type", "commerce_subscription" },
                { "subscription_id", subId.ToString() },
                { "tenant_id", sub.OrganizationId.ToString() }
            };

            if (isActiveUpdate)
            {
                metadata["update_payment"] = "1";
            }

            if (sub.CurrentDunningCampaignId != null)
            {
                metadata["dunning_campaign_id"] = sub.CurrentDunningCampaignId.ToString()!;
            }

            // Use the subscription product's gateway (not default BILLPLZ).
            string? productGateway = sub.ProductGatewayName;
            if (string.IsNullOrWhiteSpace(productGateway))
            {
                productGateway = null;
            }

            try
            {
                var checkoutQuery = new GenerateCheckoutSessionQuery(
                    sub.OrganizationId,
                    chargeAmount,
                    sub.Currency,
                    isActiveUpdate ? $"{sub.ProductName} (verification)" : sub.ProductName,
                    profile.Email,
                    successUrl,
                    cancelUrl,
                    metadata,
                    true,
                    1,
                    productGateway
                );

                var checkoutUrl = await mediator.Send(checkoutQuery);

                if (isActiveUpdate)
                {
                    await Dapper.SqlMapper.ExecuteAsync(connection, @"
                        UPDATE commerce.""Subscriptions""
                        SET ""CurrentRenewalCheckoutUrl"" = @Url, ""CurrentRenewalCheckoutForDate"" = @ForDate
                        WHERE ""Id"" = @SubId",
                        new { Url = checkoutUrl, ForDate = DateTime.UtcNow.Date, SubId = subId });
                }

                return TypedResults.Ok(new CheckoutResponse { Url = checkoutUrl, Is_zero_amount_bypass = false });
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        return group;
    }

    private sealed class ArrearsGetRow
    {
        public string ProductName { get; init; } = "";
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "";
        public string Status { get; init; } = "";
        public string? ProductGatewayName { get; init; }
    }

    /// <summary>
    /// Commerce-schema projection for public update-payment (no CRM/One columns).
    /// </summary>
    private sealed class ArrearsUpdatePaymentRow
    {
        public Guid OrganizationId { get; init; }
        public Guid ProductId { get; init; }
        public Guid ClientProfileId { get; init; }
        public string Status { get; init; } = "";
        public Guid? CurrentDunningCampaignId { get; init; }
        public string? CurrentRenewalCheckoutUrl { get; init; }
        public DateTime? CurrentRenewalCheckoutForDate { get; init; }
        public DateTime? NextBillingDate { get; init; }
        public string ProductName { get; init; } = "";
        public decimal Price { get; init; }
        public string Currency { get; init; } = "";
        public string? ProductGatewayName { get; init; }
    }
}
