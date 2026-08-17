using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Entities;

namespace Modules.Billing.Infrastructure.Commands;

public class GenerateNextSequenceNumberCommandHandler : ICommandHandler<GenerateNextSequenceNumberCommand, string>
{
    private readonly BillingDbContext _db;

    public GenerateNextSequenceNumberCommandHandler(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<string> Handle(GenerateNextSequenceNumberCommand request, CancellationToken ct)
    {
        // Same DbContext as the ledger SaveChanges. Callers wrap both in
        // IBillingTransactional so a failed persist rolls the increment back.
        var seq = await _db.DocumentSequences
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.OrganizationId == request.OrganizationId && s.Prefix == request.Prefix,
                ct);

        if (seq is null)
        {
            seq = new DocumentSequence(request.OrganizationId, request.Prefix);
            seq.Increment();
            _db.DocumentSequences.Add(seq);
        }
        else
        {
            seq.Increment();
        }

        await _db.SaveChangesAsync(ct);
        return $"{request.Prefix}-{seq.CurrentValue:D5}";
    }
}
