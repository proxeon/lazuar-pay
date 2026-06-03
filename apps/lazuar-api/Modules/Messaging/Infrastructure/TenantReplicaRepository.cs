using Microsoft.EntityFrameworkCore;
using Modules.Messaging.Domain;
using Modules.Messaging.Application;

namespace Modules.Messaging.Infrastructure;

public class TenantReplicaRepository : ITenantReplicaRepository
{
    private readonly MessagingDbContext _context;

    public TenantReplicaRepository(MessagingDbContext context)
    {
        _context = context;
    }

    public async Task<TenantReplica?> GetByIdAsync(Guid id)
    {
        return await _context.TenantReplicas.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task AddAsync(TenantReplica replica)
    {
        await _context.TenantReplicas.AddAsync(replica);
    }

    public void Update(TenantReplica replica)
    {
        _context.TenantReplicas.Update(replica);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
