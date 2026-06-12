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
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/webhooks/payments");

        // The URL explicitly contains the Tenant ID. Stripe/Billplz sends webhooks here!
        group.MapPost("/{gatewayType}/{tenantId:guid}", async (
            string gatewayType,
            Guid tenantId,
            HttpContext context,
            IMediator mediator,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PaymentWebhooks");

            // Safely read raw body by enabling buffering
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Reset position for any downstream middleware

            if (string.IsNullOrEmpty(rawBody))
            {
                logger.LogWarning("Webhook rejected for tenant {TenantId}: Empty request body.", tenantId);
                return Results.BadRequest(new { error = "Empty request body" });
            }

            // Extract headers
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in context.Request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }

            try
            {
                var command = new ProcessGatewayWebhookCommand(
                    TenantId: tenantId,
                    GatewayType: gatewayType.ToUpperInvariant(),
                    RawBody: rawBody,
                    Headers: headers
                );

                // Execute the CQRS command
                await mediator.Send(command);

                // Always return 200 OK so the gateway doesn't retry infinitely
                return Results.Ok(new { received = true });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Webhook validation failed for tenant {TenantId}. Gateway: {Gateway}. Error: {Error}", tenantId, gatewayType, ex.Message);
                // Return 400 Bad Request for validation or signature errors to halt retries
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected critical error processing webhook for tenant {TenantId}.", tenantId);
                throw; // Let GlobalExceptionHandler return 500
            }
        });

        return endpoints;
    }
}
