using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Domain.Entities;

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

    public async Task<TaxDocument?> GetTaxDocumentByInternalIdAsync(Guid organizationId, string internalReferenceId, CancellationToken ct = default)
    {
        return await _context.TaxDocuments
            .FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.InternalReferenceId == internalReferenceId, ct);
    }

    public void AddTaxDocument(TaxDocument document)
    {
        _context.TaxDocuments.Add(document);
    }

    public async Task<IEnumerable<WebhookSubscription>> GetActiveWebhooksAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _context.WebhookSubscriptions
            .Where(w => w.OrganizationId == organizationId && w.IsActive)
            .ToListAsync(ct);
    }

    public void AddWebhookSubscription(WebhookSubscription subscription)
    {
        _context.WebhookSubscriptions.Add(subscription);
    }

    public async Task<DeveloperApiKey?> GetDeveloperApiKeyAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.DeveloperApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
    }

    public async Task<IReadOnlyList<DeveloperApiKey>> ListDeveloperApiKeysAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _context.DeveloperApiKeys
            .Where(k => k.OrganizationId == organizationId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public void AddDeveloperApiKey(DeveloperApiKey key)
    {
        _context.DeveloperApiKeys.Add(key);
    }

    public async Task<IdempotencyLog?> GetIdempotencyLogAsync(Guid organizationId, string key, CancellationToken ct = default)
    {
        return await _context.IdempotencyLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.OrganizationId == organizationId && l.IdempotencyKey == key, ct);
    }

    public void AddIdempotencyLog(IdempotencyLog log)
    {
        _context.IdempotencyLogs.Add(log);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
