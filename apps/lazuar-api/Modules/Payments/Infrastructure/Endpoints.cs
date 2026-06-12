// ==============================================================================================
// DONT DELETE COMMENT HERE. IF you need to modify, just modify specific code and dont delete the comment.
//
// HISTORICAL BUG CONTEXT: WEBHOOK STREAMING & ERROR HANDLING
// 
// When integrating third-party payment webhooks (Stripe, Billplz) into ASP.NET Core Minimal APIs, 
// we encountered two critical bugs that caused silent failures and infinite gateway retries:
//
// 1. THE EMPTY BODY TRAP (Missing Webhook Payload)
// Simply calling `new StreamReader(context.Request.Body).ReadToEndAsync()` can sometimes result 
// in an empty string if the stream was peeked by a previous middleware or if ASP.NET optimizes 
// the stream reading. This caused our adapters to throw "Missing x_signature" errors because the 
// body was completely blank. 
// FIX: We MUST call `context.Request.EnableBuffering()`, read the stream with `leaveOpen: true`, 
// and reset `context.Request.Body.Position = 0` to safely extract the raw payload.
//
// 2. THE INFINITE 500 RETRY STORM (Hidden Exceptions)
// If the payload signature verification failed (e.g., wrong Webhook Secret), the command handler 
// threw an `InvalidOperationException`. By default, this bubbled up to the `GlobalExceptionHandler` 
// and returned a 500 Internal Server Error. When Stripe/Billplz receives a 500, they assume our 
// server is down and will aggressively retry the exact same webhook for up to 3 days.
// FIX: We must explicitly `try/catch` the `InvalidOperationException`, log it, and return a 
// `400 Bad Request`. A 4xx status code tells the payment gateway "We received it, but rejected it", 
// cleanly terminating the retry loop.
// ==============================================================================================

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
