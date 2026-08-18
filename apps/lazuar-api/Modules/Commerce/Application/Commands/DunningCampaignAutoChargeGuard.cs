using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Domain;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Payments.Contracts;

namespace Modules.Commerce.Application.Commands;

internal static class DunningCampaignAutoChargeGuard
{
    public const string NotAvailableMessage = "AUTO_CHARGE is not available for Billplz / reminder-only products";

    public static bool IsAutoChargeStep(DunningStepData step)
    {
        var t = (step.ActionType ?? "").Trim().ToUpperInvariant();
        return t is "AUTO_CHARGE" or "AUTOCHARGE";
    }

    public static async Task EnsureAllowedAsync(
        ICommerceRepository repository,
        Guid organizationId,
        IReadOnlyCollection<Guid>? targetProductIds,
        IReadOnlyCollection<string>? targetPaymentMethods,
        IEnumerable<DunningStepData> steps,
        CancellationToken ct)
    {
        if (!steps.Any(IsAutoChargeStep))
        {
            return;
        }

        var methods = targetPaymentMethods?.ToList() ?? new List<string>();
        if (methods.Count > 0
            && methods.All(m => string.Equals(m.Trim(), "MANUAL", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(NotAvailableMessage));
        }

        var productIds = targetProductIds?.ToList() ?? new List<Guid>();
        var products = productIds.Count == 0
            ? await repository.ListProductsAsync(organizationId, ct)
            : await repository.GetProductsByIdsAsync(organizationId, productIds, ct);

        // Empty catalog: allow (they may add Stripe later). All reminder-only: refuse (B03-C22).
        if (products.Count > 0
            && products.All(p => !PaymentGatewayCapabilities.SupportsOffSession(p.GatewayName)))
        {
            throw new BusinessRuleValidationException(new GenericBusinessRule(NotAvailableMessage));
        }
    }
}
