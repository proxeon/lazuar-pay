using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.Payments.Domain.Aggregates;

namespace Modules.Payments.Application.Ports;

public interface IIntegrationCheckoutSessionRepository
{
    Task<IntegrationCheckoutSession?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    Task<IntegrationCheckoutSession?> GetByIdempotencyKeyAsync(
        Guid organizationId,
        string idempotencyKey,
        CancellationToken ct = default);

    void Add(IntegrationCheckoutSession session);

    Task SaveChangesAsync(CancellationToken ct = default);
}
