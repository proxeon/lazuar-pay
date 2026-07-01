using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Communications.Application;
using Modules.Communications.Domain.Aggregates;

namespace Modules.Communications.Infrastructure.Repositories;

public class CommunicationsRepository : ICommunicationsRepository
{
    private readonly CommunicationsDbContext _context;

    public CommunicationsRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public async Task<MessageTemplate?> GetTemplateByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        return await _context.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == organizationId, ct);
    }

    public async Task<MessageTemplate?> GetTemplateByNameAsync(Guid organizationId, string name, CancellationToken ct = default)
    {
        return await _context.MessageTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Name == name && t.OrganizationId == organizationId, ct);
    }

    public void AddTemplate(MessageTemplate template)
    {
        _context.MessageTemplates.Add(template);
    }

    public async Task<Broadcast?> GetBroadcastByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        return await _context.Broadcasts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == organizationId, ct);
    }

    public void AddBroadcast(Broadcast broadcast)
    {
        _context.Broadcasts.Add(broadcast);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
