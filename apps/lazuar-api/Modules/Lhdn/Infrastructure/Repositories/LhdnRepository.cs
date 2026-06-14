using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Repositories;

public class LhdnRepository : ILhdnRepository
{
    private readonly LhdnDbContext _context;

    public LhdnRepository(LhdnDbContext context)
    {
        _context = context;
    }

    public async Task<LhdnTenantConfig?> GetTenantConfigAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _context.TenantConfigs.FirstOrDefaultAsync(c => c.OrganizationId == organizationId, ct);
    }

    public async Task<TaxDocument?> GetTaxDocumentAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TaxDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public void AddTaxDocument(TaxDocument document)
    {
        _context.TaxDocuments.Add(document);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
