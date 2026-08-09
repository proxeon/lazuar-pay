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
                SELECT p.""Name"" as ProductName, p.""Price"" as Amount, p.""Currency"", s.""Status""
                FROM commerce.""Subscriptions"" s
                JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
                WHERE s.""Id"" = @SubId LIMIT 1";
                
            var result = await Dapper.SqlMapper.QuerySingleOrDefaultAsync<ArrearsSummaryDto>(connection, query, new { SubId = subId });
            return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
        });

        group.MapPost("/checkout/{subId:guid}/update-payment", async Task<Results<Ok<CheckoutResponse>, BadRequest<string>>> (
            Guid subId,
            [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory sqlFactory,
            IMediator mediator,
            IConfiguration config) =>
        {
            using var connection = sqlFactory.CreateConnection();
            var query = @"
                SELECT s.""OrganizationId"", s.""ProductId"", s.""Status"", s.""CurrentDunningCampaignId"",
                       p.""Name"" as ProductName, p.""Price"", p.""Currency"", p.""GatewayName"" as ProductGatewayName,
                       cp.""Email"" as CustomerEmail,
                       org.""Slug"" as TenantSlug
                FROM commerce.""Subscriptions"" s
                JOIN commerce.""Products"" p ON s.""ProductId"" = p.""Id""
                JOIN crm.""ClientProfiles"" cp ON s.""ClientProfileId"" = cp.""Id""
                JOIN one.""Organizations"" org ON s.""OrganizationId"" = org.""Id""
                WHERE s.""Id"" = @SubId LIMIT 1";
                
            var sub = await Dapper.SqlMapper.QuerySingleOrDefaultAsync<dynamic>(connection, query, new { SubId = subId });
            
            if (sub == null) return TypedResults.BadRequest("Subscription not found.");
            if (sub.Status != "PAST_DUE" && sub.Status != "SUSPENDED") return TypedResults.BadRequest("This subscription is currently active and does not require a payment update.");

            var clientUrl = config["App:ClientUrl"]?.TrimEnd('/') ?? "http://localhost:3004";
            var successUrl = $"{clientUrl}/{sub.TenantSlug}/portal"; 
            var cancelUrl = $"{clientUrl}/{sub.TenantSlug}/update-payment/{subId}";

            var metadata = new Dictionary<string, string>
            {
                { "type", "commerce_subscription" },
                { "subscription_id", subId.ToString() },
                { "tenant_id", sub.OrganizationId.ToString() }
            };

            if (sub.CurrentDunningCampaignId != null)
            {
                metadata["dunning_campaign_id"] = sub.CurrentDunningCampaignId.ToString();
            }

            // Use the subscription product's gateway (not default BILLPLZ).
            string? productGateway = sub.ProductGatewayName as string;
            if (string.IsNullOrWhiteSpace(productGateway))
            {
                productGateway = null;
            }

            try
            {
                var checkoutQuery = new GenerateCheckoutSessionQuery(
                    (Guid)sub.OrganizationId,
                    (decimal)sub.Price,
                    (string)sub.Currency,
                    (string)sub.ProductName,
                    (string)sub.CustomerEmail,
                    successUrl,
                    cancelUrl,
                    metadata,
                    true, 
                    1,
                    productGateway
                );

                var checkoutUrl = await mediator.Send(checkoutQuery);
                return TypedResults.Ok(new CheckoutResponse { Url = checkoutUrl, Is_zero_amount_bypass = false });
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        return group;
    }
}
