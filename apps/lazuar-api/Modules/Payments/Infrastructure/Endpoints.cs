using System.Text;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Commands;

namespace Modules.Payments.Infrastructure;

public static class Endpoints
{
    private static readonly HashSet<string> AllowedGatewayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "STRIPE",
        "BILLPLZ",
        "RAZORPAY",
        "CHIP",
        "XENDIT"
    };

    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/webhooks/payments");

        group.MapPost("/{gatewayType}/{tenantId:guid}", async (
            string gatewayType,
            Guid tenantId,
            HttpContext context,
            IMediator mediator,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PaymentWebhooks");

            if (!AllowedGatewayTypes.Contains(gatewayType))
            {
                return Results.BadRequest(new { error = $"Unsupported payment gateway type '{gatewayType}'." });
            }

            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                // Health checks / empty retries must not 500 (B04-P18).
                return Results.BadRequest(new { error = "Empty request body." });
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in context.Request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }

            foreach (var query in context.Request.Query)
            {
                headers[$"Query-{query.Key}"] = query.Value.ToString();
            }

            try
            {
                var command = new ProcessGatewayWebhookCommand(
                    TenantId: tenantId,
                    GatewayType: gatewayType.ToUpperInvariant(),
                    RawBody: rawBody,
                    Headers: headers
                );

                await mediator.Send(command);

                // Intake ACK only. Domain fulfillment lives in Commerce / Billing / M2M session.
                return Results.Ok(new { received = true });
            }
            catch (PaymentWebhookUnusablePayloadException ex)
            {
                // Verified signature, unusable body (missing id/currency). ACK 400 so the gateway stops.
                logger.LogWarning(ex, "Unusable payment webhook payload for tenant {TenantId}.", tenantId);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is NotSupportedException
                || (ex is InvalidOperationException ioe
                    && ioe.Message.Contains("is not supported", StringComparison.OrdinalIgnoreCase)))
            {
                // Unknown gateway must not 500 (gateway retries forever). Allow-list is primary; this is defense-in-depth.
                logger.LogWarning(ex, "Rejected unsupported gateway type {GatewayType} for tenant {TenantId}.", gatewayType, tenantId);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is not InvalidOperationException && ex is not BuildingBlocks.Domain.BusinessRuleValidationException)
            {
                logger.LogError(ex, "Unexpected critical error processing webhook for tenant {TenantId}.", tenantId);
                throw;
            }
        });

        return endpoints;
    }
}
