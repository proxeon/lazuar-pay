using Modules.Messaging.Domain;

namespace Modules.Messaging.Application;

public interface ITenantReplicaRepository
{
    Task<TenantReplica?> GetByIdAsync(Guid id);
    Task AddAsync(TenantReplica replica);
    void Update(TenantReplica replica);
    Task SaveChangesAsync();
}
