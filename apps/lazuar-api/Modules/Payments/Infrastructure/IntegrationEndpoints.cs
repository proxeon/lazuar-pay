using System;
using System.Collections.Generic;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Payments.Application.Exceptions;
using Modules.Payments.Contracts.Commands;
using Modules.Payments.Contracts.Queries;

namespace Modules.Payments.Infrastructure;

public static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // No trailing slash: OpenAPI + clients use /integrations/payments/checkouts
        var group = endpoints.MapGroup("/integrations/payments")
            .RequireCors();

        group.MapPost("/checkouts", async (
            CreateIntegrationCheckoutRequestDto body,
            HttpContext http,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.TenantId == Guid.Empty)
            {
                return Results.Json(
                    Problem(PaymentErrorCodes.Unauthorized, "Missing or invalid authentication.", 401),
                    statusCode: 401);
            }

            var idempotencyKey = ResolveIdempotencyKey(http, body.Idempotency_key);

            try
            {
                var result = await mediator.Send(new CreateIntegrationCheckoutCommand(
                    OrganizationId: ctx.TenantId,
                    Amount: (decimal)body.Amount,
                    Currency: body.Currency ?? string.Empty,
                    Description: body.Description ?? string.Empty,
                    CustomerEmail: body.Customer_email ?? string.Empty,
                    SuccessUrl: body.Success_url ?? string.Empty,
                    CancelUrl: body.Cancel_url ?? string.Empty,
                    CustomerName: body.Customer_name,
                    GatewayName: body.Gateway_name,
                    SetupFutureUsage: body.Setup_future_usage ?? false,
                    IdempotencyKey: idempotencyKey,
                    Metadata: body.Metadata));

                return Results.Ok(ToResponse(result));
            }
            catch (PaymentIntegrationException ex)
            {
                return Results.Json(Problem(ex.Code, ex.Message, ex.StatusCode), statusCode: ex.StatusCode);
            }
        })
        .RequireAuthorization("IntegrationPaymentsCheckoutsWrite");

        group.MapGet("/checkouts/{checkoutId:guid}", async (
            Guid checkoutId,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.TenantId == Guid.Empty)
            {
                return Results.Json(
                    Problem(PaymentErrorCodes.Unauthorized, "Missing or invalid authentication.", 401),
                    statusCode: 401);
            }

            var result = await mediator.Send(new GetIntegrationCheckoutQuery(ctx.TenantId, checkoutId));
            if (result == null)
            {
                return Results.Json(
                    Problem(PaymentErrorCodes.CheckoutNotFound, "Checkout session not found.", 404),
                    statusCode: 404);
            }

            return Results.Ok(ToResponse(result));
        })
        .RequireAuthorization("IntegrationPaymentsCheckoutsRead");

        return endpoints;
    }

    private static string? ResolveIdempotencyKey(HttpContext http, string? bodyKey)
    {
        if (http.Request.Headers.TryGetValue("Idempotency-Key", out var header)
            && !string.IsNullOrWhiteSpace(header.ToString()))
        {
            return header.ToString().Trim();
        }

        return string.IsNullOrWhiteSpace(bodyKey) ? null : bodyKey.Trim();
    }

    private static IntegrationCheckoutResponseDto ToResponse(IntegrationCheckoutResult result) =>
        new()
        {
            Checkout_id = result.CheckoutId.ToString(),
            Checkout_url = result.CheckoutUrl,
            Gateway = result.Gateway,
            Status = result.Status,
            Amount = (double)result.Amount,
            Currency = result.Currency,
            Provider_session_id = result.ProviderSessionId,
            Gateway_transaction_id = result.GatewayTransactionId,
            Expires_at = new DateTimeOffset(DateTime.SpecifyKind(result.ExpiresAt, DateTimeKind.Utc)),
            Metadata = result.Metadata ?? new Dictionary<string, string>()
        };

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Problem(string code, string detail, int status) =>
        new()
        {
            Status = status,
            Title = code,
            Detail = detail,
            Extensions = { ["code"] = code }
        };
}
