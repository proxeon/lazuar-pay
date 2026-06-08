using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.One.Application;
using Modules.One.Domain;

namespace Modules.One.Infrastructure.Repositories;

public class OneRepository : IOneRepository
{
    private readonly OneDbContext _context;

    public OneRepository(OneDbContext context)
    {
        _context = context;
    }

    public void AddOrganization(Organization organization) => _context.Organizations.Add(organization);
    
    public void AddTenantMembership(TenantMembership membership) => _context.TenantMemberships.Add(membership);
    
    public void RemoveTenantMembership(TenantMembership membership) => _context.TenantMemberships.Remove(membership);
    
    public void AddEntitlement(TenantAppEntitlement entitlement) => _context.TenantAppEntitlements.Add(entitlement);

    public async Task<TenantAppEntitlement?> GetEntitlementAsync(Guid organizationId, string appId, CancellationToken ct = default)
    {
        return await _context.TenantAppEntitlements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.OrganizationId == organizationId && e.AppId == appId, ct);
    }

    public async Task<bool> HasMembershipAsync(Guid globalUserId, Guid organizationId, CancellationToken ct = default)
    {
        return await _context.TenantMemberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.GlobalUserId == globalUserId && m.OrganizationId == organizationId, ct);
    }

    public async Task<TenantMembership?> GetMembershipAsync(Guid globalUserId, Guid organizationId, CancellationToken ct = default)
    {
        return await _context.TenantMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.GlobalUserId == globalUserId && m.OrganizationId == organizationId, ct);
    }

    public async Task<GlobalUser?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.GlobalUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<GlobalUser?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.GlobalUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
    }

    public void AddGlobalUser(GlobalUser user) => _context.GlobalUsers.Add(user);

    public void AddWorkspaceInvitation(WorkspaceInvitation invitation) => _context.WorkspaceInvitations.Add(invitation);

    public async Task<WorkspaceInvitation?> GetInvitationByHashAsync(string hash, CancellationToken ct = default)
    {
        return await _context.WorkspaceInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
