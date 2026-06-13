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

        group.MapPost("/{gatewayType}/{tenantId:guid}", async (
            string gatewayType,
            Guid tenantId,
            HttpContext context,
            IMediator mediator,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PaymentWebhooks");

            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; 

            if (string.IsNullOrEmpty(rawBody))
            {
                logger.LogWarning("Webhook rejected for tenant {TenantId}: Empty request body.", tenantId);
                return Results.BadRequest(new { error = "Empty request body" });
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

                return Results.Ok(new { received = true });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Webhook validation failed for tenant {TenantId}. Gateway: {Gateway}. Error: {Error}", tenantId, gatewayType, ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected critical error processing webhook for tenant {TenantId}.", tenantId);
                throw; 
            }
        });

        return endpoints;
    }
}
