using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;

namespace Modules.One.Infrastructure.Services;

public class OneQueryService : IOneQueryService
{
    private readonly OneDbContext _context;
    private readonly ISqlConnectionFactory _connectionFactory;

    public OneQueryService(
        OneDbContext context, 
        [FromKeyedServices("OneSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _context = context;
        _connectionFactory = connectionFactory;
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
        var query = await _context.TenantMemberships
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.OrganizationId == tenantId)
            .Join(_context.GlobalUsers.AsNoTracking(), 
                  m => m.GlobalUserId, 
                  u => u.Id, 
                  (m, u) => new { m, u })
            .OrderBy(x => x.m.CreatedAt)
            .ToListAsync();

        return query.Select(x => new WorkspaceMemberSnapshotDto(
            x.m.Id, 
            x.m.GlobalUserId, 
            x.u.Name, 
            x.u.Email, 
            x.m.Role, 
            x.m.CreatedAt));
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

    public async Task<IEnumerable<MyPendingInvitationDto>> GetMyPendingInvitationsAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                i.""Id"", 
                o.""Name"" as WorkspaceName, 
                i.""Role"", 
                i.""ExpiresAt""
            FROM one.""WorkspaceInvitations"" i
            JOIN one.""Organizations"" o ON i.""OrganizationId"" = o.""Id""
            WHERE i.""Email"" = @Email AND i.""Status"" = 'PENDING' AND i.""ExpiresAt"" > NOW()
            ORDER BY i.""CreatedAt"" DESC";

        var results = await connection.QueryAsync<dynamic>(sql, new { Email = email.Trim().ToLowerInvariant() });

        return results.Select(r => new MyPendingInvitationDto
        {
            Id = r.Id.ToString(),
            Workspace_name = r.workspacename,
            Role = r.role,
            Expires_at = new DateTimeOffset(r.expiresat)
        });
    }
}
