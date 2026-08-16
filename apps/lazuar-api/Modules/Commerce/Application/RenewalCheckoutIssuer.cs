using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Contracts;
using Modules.Commerce.Contracts;
using Modules.Commerce.Domain.Aggregates;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Queries;

namespace Modules.Commerce.Application;

/// <summary>
/// Mints a hosted checkout bound to an existing subscription id (not a new Commerce session).
/// </summary>
public static class RenewalCheckoutIssuer
{
    public static async Task<string> MintAsync(
        IMediator mediator,
        IOneQueryService one,
        IConfiguration? config,
        IMagicLinkTokenService tokenService,
        Subscription sub,
        Product product,
        string customerEmail,
        CancellationToken ct,
        IBillingQueryService? billing = null)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerEmail);

        var workspace = await one.GetWorkspaceByIdAsync(sub.OrganizationId)
            ?? throw new InvalidOperationException(
                $"Workspace {sub.OrganizationId} not found for renewal checkout.");

        var clientUrl = config?["App:ClientUrl"]?.TrimEnd('/') ?? "http://localhost:3004";
        var successUrl = $"{clientUrl}/{workspace.Slug}/portal";
        var magicToken = tokenService.GenerateToken(sub.Id);
        var cancelUrl = $"{clientUrl}/{workspace.Slug}/update-payment/{sub.Id}?token={magicToken}";

        var metadata = new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = sub.Id.ToString(),
            ["tenant_id"] = sub.OrganizationId.ToString()
        };

        var url = await mediator.Send(new GenerateCheckoutSessionQuery(
            sub.OrganizationId,
            await SubscriptionBillingAmount.Gross(sub, product, billing),
            product.Currency,
            product.Name,
            customerEmail,
            successUrl,
            cancelUrl,
            metadata,
            SetupFutureUsage: true,
            Quantity: 1,
            GatewayName: product.GatewayName), ct);

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("GenerateCheckoutSessionQuery returned an empty renewal checkout URL.");
        }

        return url;
    }
}
