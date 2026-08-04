// apps/lazuar-api/Modules/Billing/Infrastructure/Commands/DeductTenantCreditCommandHandler.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Domain.Entities;
using Npgsql;

namespace Modules.Billing.Infrastructure.Commands;

public class DeductTenantCreditCommandHandler : ICommandHandler<DeductTenantCreditCommand>
{
    private readonly BillingDbContext _dbContext;
    private const int MaxAttempts = 3;

    public DeductTenantCreditCommandHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeductTenantCreditCommand request, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // Re-check idempotency on every attempt: a concurrent request may have already
            // committed the deduction for this key.
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                var existing = await _dbContext.CreditDeductionIdempotencyLogs
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.OrganizationId == request.OrganizationId
                        && x.IdempotencyKey == request.IdempotencyKey, ct);
                if (existing != null)
                    return;
            }

            var wallet = await _dbContext.TenantCreditBalances
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId, ct);

            if (wallet == null)
                throw new InvalidOperationException("Tenant credit wallet not found.");

            try
            {
                // Throws on insufficient balance (domain enforces sufficiency).
                wallet.Deduct(request.Amount, request.Reference);

                if (!string.IsNullOrEmpty(request.IdempotencyKey))
                {
                    _dbContext.CreditDeductionIdempotencyLogs.Add(
                        new CreditDeductionIdempotencyLog(
                            request.OrganizationId, request.IdempotencyKey, request.Amount, request.Reference));
                }

                await _dbContext.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Row-version conflict: another concurrent deduction modified the wallet. Retry.
                _dbContext.ChangeTracker.Clear();
                if (attempt == MaxAttempts - 1) throw;
            }
            catch (DbUpdateException ex) when (
                !string.IsNullOrEmpty(request.IdempotencyKey)
                && IsUniqueViolation(ex))
            {
                // Concurrent same-key race: peer already inserted the idempotency log.
                // Treat as success (no double charge). Unique index is the safety net.
                _dbContext.ChangeTracker.Clear();
                return;
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;
        }

        return false;
    }
}
