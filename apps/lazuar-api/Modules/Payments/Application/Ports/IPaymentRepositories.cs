using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Domain.Entities;

namespace Modules.Payments.Application.Ports;

public interface ITenantPaymentConfigRepository
{
    Task<TenantPaymentConfiguration?> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
}

public interface IPaymentWebhookLogRepository
{
    Task<bool> HasBeenProcessedAsync(string eventId, string provider, CancellationToken ct = default);
    void Add(PaymentWebhookLog log);

    /// <summary>
    /// Saves changes to the database, committing both the log and the outbox messages transactionally.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
