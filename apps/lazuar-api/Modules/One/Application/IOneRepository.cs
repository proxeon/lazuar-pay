using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.One.Domain;

namespace Modules.One.Application;

public interface IOneRepository
{
    void AddOrganization(Organization organization);
    Task<Organization?> GetOrganizationByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> IsSlugUniqueAsync(string slug, Guid currentOrganizationId, CancellationToken ct = default);

    void AddTenantMembership(TenantMembership membership);
    void RemoveTenantMembership(TenantMembership membership);
    void AddEntitlement(TenantAppEntitlement entitlement);

    Task<TenantAppEntitlement?> GetEntitlementAsync(Guid organizationId, string appId, CancellationToken ct = default);
    Task<bool> HasMembershipAsync(Guid globalUserId, Guid organizationId, CancellationToken ct = default);
    Task<TenantMembership?> GetMembershipAsync(Guid globalUserId, Guid organizationId, CancellationToken ct = default);

    Task<GlobalUser?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<GlobalUser?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    void AddGlobalUser(GlobalUser user);

    void AddWorkspaceInvitation(WorkspaceInvitation invitation);
    Task<WorkspaceInvitation?> GetInvitationByHashAsync(string hash, CancellationToken ct = default);
    Task<WorkspaceInvitation?> GetInvitationByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasPendingInvitationAsync(string email, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
