// apps/lazuar-api/Modules/Ops/Infrastructure/Repositories/OpsRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Ops.Application;
using Modules.Ops.Domain;

namespace Modules.Ops.Infrastructure.Repositories;

public class OpsRepository : IOpsRepository
{
    private readonly OpsDbContext _context;

    public OpsRepository(OpsDbContext context)
    {
        _context = context;
    }

    public async Task<OpsConversation?> GetConversationByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        return await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId, ct);
    }

    public async Task<IEnumerable<OpsConversation>> GetConversationsAsync(Guid organizationId, int limit, int offset, CancellationToken ct = default)
    {
        return await _context.Conversations
            .Where(c => c.OrganizationId == organizationId)
            .OrderByDescending(c => c.UpdatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<OpsMessage?> GetMessageByIdAsync(Guid organizationId, Guid messageId, CancellationToken ct = default)
    {
        return await _context.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.OrganizationId == organizationId, ct);
    }

    public async Task<IEnumerable<OpsMessage>> GetMessagesAsync(Guid organizationId, Guid conversationId, CancellationToken ct = default)
    {
        return await _context.Messages
            .Where(m => m.OrganizationId == organizationId && m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public void AddConversation(OpsConversation conversation) => _context.Conversations.Add(conversation);

    public void AddMessage(OpsMessage message) => _context.Messages.Add(message);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
