// apps/lazuar-api/Modules/Billing/Application/Commands/DeductTenantCreditCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Infrastructure;

namespace Modules.Billing.Application.Commands;

public record DeductTenantCreditCommand(Guid OrganizationId, int Amount, string Reference) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class DeductTenantCreditCommandHandler : ICommandHandler<DeductTenantCreditCommand>
{
    private readonly BillingDbContext _dbContext;

    public DeductTenantCreditCommandHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeductTenantCreditCommand request, CancellationToken ct)
    {
        var wallet = await _dbContext.TenantCreditBalances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId, ct);

        if (wallet == null)
        {
            throw new InvalidOperationException("Tenant credit wallet not found.");
        }

        wallet.Deduct(request.Amount, request.Reference);
        await _dbContext.SaveChangesAsync(ct);
    }
}
