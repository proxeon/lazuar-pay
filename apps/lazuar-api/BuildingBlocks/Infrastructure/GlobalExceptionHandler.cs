using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Prefer endpoint-local ProblemDetails with stable `code` extensions for public M2M APIs
        // (exemplar: Payments IntegrationEndpoints). This handler is the last-resort mapping for
        // uncaught domain/validation throws so clients still get RFC 7807 JSON.
        ProblemDetails problemDetails;

        if (exception is BusinessRuleValidationException)
        {
            _logger.LogWarning("Business rule failed: {Message}", exception.Message);

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Business Rule Violation",
                Detail = exception.Message,
                Extensions = { ["code"] = "business_rule_violation" }
            };
        }
        else if (exception is InvalidOperationException)
        {
            _logger.LogWarning("Validation failed: {Message}", exception.Message);

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = exception.Message,
                Extensions = { ["code"] = "invalid_operation" }
            };
        }
        else
        {
            _logger.LogError(exception, "Unhandled exception caught by GlobalExceptionHandler.");

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
                Detail = "An unexpected error occurred.",
                Extensions = { ["code"] = "internal_error" }
            };
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
