using System.Text;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
            IMediator mediator) =>
        {
            // Read raw body
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync();

            // Extract headers
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in context.Request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }

            // Append query string parameters to the headers dictionary so that stateless adapters (like Billplz)
            // can read checkout metadata from the callback URL itself.
            foreach (var queryParam in context.Request.Query)
            {
                headers[$"Query-{queryParam.Key}"] = queryParam.Value.ToString();
            }

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
        });

        return endpoints;
    }
}
