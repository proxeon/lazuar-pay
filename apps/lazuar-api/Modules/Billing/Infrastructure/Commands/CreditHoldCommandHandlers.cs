// apps/lazuar-api/Modules/Billing/Infrastructure/Commands/CreditHoldCommandHandlers.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.Commands;

public class ReserveCreditsCommandHandler : ICommandHandler<ReserveCreditsCommand, Guid>
{
    private readonly BillingDbContext _dbContext;
    private const int MaxAttempts = 3;

    public ReserveCreditsCommandHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(ReserveCreditsCommand request, CancellationToken ct)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Reserve amount must be positive.");

        var existing = await _dbContext.CreditHolds
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                h => h.OrganizationId == request.OrganizationId && h.CorrelationId == request.CorrelationId,
                ct);
        if (existing != null)
            return existing.Id;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var wallet = await _dbContext.TenantCreditBalances
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId, ct);

            if (wallet == null)
                throw new InvalidOperationException("Tenant credit wallet not found.");

            try
            {
                wallet.Deduct(request.Amount, $"Reserve: {request.Reference}");
                var hold = new CreditHold(request.OrganizationId, request.Amount, request.CorrelationId, request.Reference);
                _dbContext.CreditHolds.Add(hold);
                await _dbContext.SaveChangesAsync(ct);
                return hold.Id;
            }
            catch (DbUpdateConcurrencyException)
            {
                _dbContext.ChangeTracker.Clear();
                if (attempt == MaxAttempts - 1) throw;
            }
            catch (DbUpdateException)
            {
                _dbContext.ChangeTracker.Clear();
                var raced = await _dbContext.CreditHolds
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(
                        h => h.OrganizationId == request.OrganizationId && h.CorrelationId == request.CorrelationId,
                        ct);
                if (raced != null)
                    return raced.Id;
                throw;
            }
        }

        throw new InvalidOperationException("Unable to reserve credits after retries.");
    }
}

public class ConsumeCreditHoldCommandHandler : ICommandHandler<ConsumeCreditHoldCommand>
{
    private readonly BillingDbContext _dbContext;

    public ConsumeCreditHoldCommandHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(ConsumeCreditHoldCommand request, CancellationToken ct)
    {
        var hold = await _dbContext.CreditHolds
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.Id == request.HoldId && h.OrganizationId == request.OrganizationId, ct);

        if (hold == null)
            throw new InvalidOperationException("Credit hold not found.");

        hold.Consume(request.Amount);
        await _dbContext.SaveChangesAsync(ct);
    }
}

public class ReleaseCreditHoldCommandHandler : ICommandHandler<ReleaseCreditHoldCommand, int>
{
    private readonly BillingDbContext _dbContext;
    private const int MaxAttempts = 3;

    public ReleaseCreditHoldCommandHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> Handle(ReleaseCreditHoldCommand request, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var hold = await _dbContext.CreditHolds
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(h => h.Id == request.HoldId && h.OrganizationId == request.OrganizationId, ct);

            if (hold == null)
                throw new InvalidOperationException("Credit hold not found.");
            if (hold.Status != "HELD")
                return 0;

            var released = hold.ReleaseRemaining();

            if (released > 0)
            {
                var wallet = await _dbContext.TenantCreditBalances
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId, ct);

                if (wallet != null)
                    wallet.TopUp(released, $"Release hold: {request.Reference}");
            }

            try
            {
                await _dbContext.SaveChangesAsync(ct);
                return released;
            }
            catch (DbUpdateConcurrencyException)
            {
                _dbContext.ChangeTracker.Clear();
                if (attempt == MaxAttempts - 1) throw;
            }
        }

        throw new InvalidOperationException("Unable to release credit hold after retries.");
    }
}
