using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.One.Domain;

namespace Modules.One.Application;

public interface IOneRepository
{
    void AddOrganization(Organization organization);
    void AddTenantMembership(TenantMembership membership);
    void AddEntitlement(TenantAppEntitlement entitlement);
    
    Task<TenantAppEntitlement?> GetEntitlementAsync(Guid organizationId, string appId, CancellationToken ct = default);
    Task<bool> HasMembershipAsync(Guid globalUserId, Guid organizationId, CancellationToken ct = default);
    
    Task<GlobalUser?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<GlobalUser?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    void AddGlobalUser(GlobalUser user);
    
    void AddWorkspaceInvitation(WorkspaceInvitation invitation);
    Task<WorkspaceInvitation?> GetInvitationByHashAsync(string hash, CancellationToken ct = default);
    
    Task SaveChangesAsync(CancellationToken ct = default);
}
