using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Payments.Contracts;

namespace Modules.Payments.Application.Commands;

public partial class ProcessGatewayWebhookCommandHandler
{
    /// <summary>
    /// Business key for payment-level idempotency across dual gateway events
    /// (e.g. Stripe checkout.session.completed + payment_intent.succeeded).
    /// </summary>
    private static string? BuildBusinessKey(string eventType, string? gatewayTransactionId)
    {
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return null;
        }

        // Money events only (caller already filters to these)
        return eventType + ":" + gatewayTransactionId;
    }

    internal static bool TryGetInboundTenantId(Dictionary<string, string>? metadata, out Guid tenantId)
        => TryGetMetadataGuid(metadata, "tenant_id", out tenantId);

    internal static bool TryGetMetadataGuid(Dictionary<string, string>? metadata, string key, out Guid value)
    {
        value = Guid.Empty;
        if (metadata is null
            || !metadata.TryGetValue(key, out var raw)
            || string.IsNullOrWhiteSpace(raw)
            || !Guid.TryParse(raw, out value)
            || value == Guid.Empty)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Platform checkout (Hub SaaS / credits) keeps the paying workspace in
    /// <c>tenant_id</c> and stamps the system org as <c>platform_tenant_id</c>.
    /// The webhook URL is the system org — that mismatch is expected.
    /// </summary>
    internal static bool IsPlatformCheckoutWebhook(Guid urlTenant, Dictionary<string, string>? metadata)
    {
        return urlTenant == PlatformCheckoutTypes.SystemOrganizationId
            && TryGetMetadataGuid(metadata, "platform_tenant_id", out var platformTenant)
            && platformTenant == urlTenant;
    }

    private async Task TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _logRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent delivery raced past the pre-checks; treat as successful duplicate (HTTP 200).
            return;
        }
    }

    /// <summary>
    /// Detects PostgreSQL unique_violation (SQLSTATE 23505) without hard-depending on Npgsql in Application.
    /// </summary>
    public static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (Exception? ex = exception; ex != null; ex = ex.InnerException)
        {
            var sqlState = ex.GetType().GetProperty("SqlState")?.GetValue(ex) as string;
            if (sqlState == "23505")
            {
                return true;
            }

            if (ex.Message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
