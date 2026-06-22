using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.One.Contracts;
using Lazuar.ApiTypes;

namespace Modules.One.Infrastructure.Services;

public class OneQueryService : IOneQueryService
{
    private readonly OneDbContext _context;

    public OneQueryService(OneDbContext context)
    {
        _context = context;
    }

    public async Task<WorkspaceSnapshotDto?> GetWorkspaceByIdAsync(Guid tenantId)
    {
        return await _context.Organizations
            .AsNoTracking()
            .Where(o => o.Id == tenantId)
            .Select(o => new WorkspaceSnapshotDto(o.Id, o.Name, o.Slug, o.IsActive, o.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public async Task<WorkspaceSnapshotDto?> GetWorkspaceBySlugAsync(string slug)
    {
        var normalizedSlug = slug.ToLower().Trim();
        return await _context.Organizations
            .AsNoTracking()
            .Where(o => o.Slug == normalizedSlug)
            .Select(o => new WorkspaceSnapshotDto(o.Id, o.Name, o.Slug, o.IsActive, o.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WorkspaceSnapshotDto>> GetWorkspacesAsync()
    {
        return await _context.Organizations
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new WorkspaceSnapshotDto(o.Id, o.Name, o.Slug, o.IsActive, o.CreatedAt))
            .ToListAsync();
    }

    public async Task<Guid?> GetTenantIdBySlugAsync(string slug)
    {
        var normalizedSlug = slug.ToLower().Trim();
        return await _context.Organizations
            .AsNoTracking()
            .Where(o => o.Slug == normalizedSlug && o.IsActive)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasTenantAccessAsync(Guid globalUserId, Guid tenantId)
    {
        return await _context.TenantMemberships
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(m => m.GlobalUserId == globalUserId && m.OrganizationId == tenantId);
    }

    public async Task<string?> GetTenantRoleAsync(Guid globalUserId, Guid tenantId)
    {
        return await _context.TenantMemberships
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.GlobalUserId == globalUserId && m.OrganizationId == tenantId)
            .Select(m => m.Role)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<string>> GetWorkspaceAppsAsync(Guid tenantId)
    {
        return await _context.TenantAppEntitlements
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == tenantId && e.IsActive)
            .Select(e => e.AppId)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkspaceEntitlementSnapshotDto>> GetWorkspaceEntitlementsAsync(Guid tenantId)
    {
        return await _context.TenantAppEntitlements
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == tenantId)
            .Select(e => new WorkspaceEntitlementSnapshotDto(e.AppId, e.IsActive))
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkspaceMemberSnapshotDto>> GetWorkspaceMembersAsync(Guid tenantId)
    {
        return await _context.TenantMemberships
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.OrganizationId == tenantId)
            .Join(_context.GlobalUsers.AsNoTracking(), 
                  m => m.GlobalUserId, 
                  u => u.Id, 
                  (m, u) => new WorkspaceMemberSnapshotDto(m.Id, m.GlobalUserId, u.Name, u.Email, m.Role, m.CreatedAt))
            .OrderBy(m => m.JoinedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkspaceInvitationSnapshotDto>> GetWorkspaceInvitationsAsync(Guid tenantId)
    {
        return await _context.WorkspaceInvitations
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => i.OrganizationId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new WorkspaceInvitationSnapshotDto(i.Id, i.Email, i.Role, i.Status, i.ExpiresAt))
            .ToListAsync();
    }
}
