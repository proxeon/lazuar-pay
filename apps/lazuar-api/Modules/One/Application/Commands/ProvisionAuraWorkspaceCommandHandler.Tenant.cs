using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public partial class ProvisionAuraWorkspaceCommandHandler
{
    private Organization CreateBoundOrganization(
        string displayName,
        string slug,
        string product,
        string externalOrgId)
    {
        var organization = new Organization(displayName, slug);
        organization.BindExternalRef(product, externalOrgId);
        _repository.AddOrganization(organization);
        return organization;
    }

    private async Task GrantPaymentsEntitlementAsync(Guid organizationId)
    {
        var entitlement = new TenantAppEntitlement(organizationId, PaymentsAppId);
        _repository.AddEntitlement(entitlement);
        await _eventBus.PublishAsync(
            new AppEntitlementGrantedIntegrationEvent(organizationId, PaymentsAppId));
    }

    private async Task<string> ResolveSlugAsync(
        string? requestedSlug,
        string product,
        string externalOrgId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requestedSlug))
        {
            var clean = requestedSlug.Trim().ToLowerInvariant();
            if (!await _repository.IsSlugUniqueAsync(clean, ct))
            {
                throw new InvalidOperationException(
                    "The requested workspace slug is already taken. Please choose another.");
            }

            return clean;
        }

        // {product}-{compact id} — compact, valid slug charset.
        var compact = externalOrgId.Replace("-", "", StringComparison.Ordinal);
        // Keep only slug-safe chars for non-guid ids.
        compact = Regex.Replace(compact, @"[^a-z0-9]", "", RegexOptions.IgnoreCase);
        if (compact.Length == 0)
        {
            compact = Guid.CreateVersion7().ToString("N")[..12];
        }

        var baseSlug = $"{product}-{compact[..Math.Min(12, compact.Length)]}";
        if (baseSlug.Length > 63)
        {
            baseSlug = baseSlug[..63].TrimEnd('-');
        }

        if (await _repository.IsSlugUniqueAsync(baseSlug, ct))
        {
            return baseSlug;
        }

        for (var i = 2; i <= 20; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (candidate.Length > 63)
            {
                candidate = candidate[..63].TrimEnd('-');
            }

            if (await _repository.IsSlugUniqueAsync(candidate, ct))
            {
                return candidate;
            }
        }

        // Extremely unlikely: append short random hex.
        var fallback = $"{product}-{Guid.CreateVersion7():N}"[..Math.Min(20, product.Length + 17)];
        if (!await _repository.IsSlugUniqueAsync(fallback, ct))
        {
            throw new InvalidOperationException("Unable to allocate a unique workspace slug.");
        }

        return fallback;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        // Npgsql: 23505 unique_violation. Match by message/type without hard Npgsql dependency in Application.
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var typeName = e.GetType().Name;
            if (typeName.Contains("Postgres", StringComparison.OrdinalIgnoreCase)
                && e.Message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }

            if (e.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                && (e.Message.Contains("ExternalProduct", StringComparison.OrdinalIgnoreCase)
                    || e.Message.Contains("IX_Organizations_External", StringComparison.OrdinalIgnoreCase)
                    || e.Message.Contains("23505", StringComparison.Ordinal)))
            {
                return true;
            }

            // EF InMemory / generic: DbUpdateException with inner unique.
            if (typeName.Contains("DbUpdate", StringComparison.OrdinalIgnoreCase)
                && e.InnerException is not null
                && IsUniqueViolation(e.InnerException))
            {
                return true;
            }
        }

        return false;
    }
}
