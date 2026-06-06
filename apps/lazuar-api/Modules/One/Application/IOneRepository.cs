using Modules.One.Domain;

namespace Modules.One.Application;

public interface IOneRepository
{
    void AddOrganization(Organization organization);
    void AddTenantMembership(TenantMembership membership);
    Task<bool> HasMembershipAsync(Guid globalUserId, Guid organizationId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
