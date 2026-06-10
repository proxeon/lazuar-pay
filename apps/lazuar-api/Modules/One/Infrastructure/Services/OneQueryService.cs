using Dapper;
using System.Data;
using System.Text.Json;
using Modules.One.Contracts;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Lazuar.ApiTypes;

namespace Modules.One.Infrastructure.Services;

public class OneQueryService : IOneQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public OneQueryService([FromKeyedServices("OneSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<WorkspaceSnapshotDto?> GetWorkspaceByIdAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\", \"CreatedAt\" FROM one.\"Organizations\" WHERE \"Id\" = @Id LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<WorkspaceSnapshotDto>(sql, new { Id = tenantId });
    }

    public async Task<WorkspaceSnapshotDto?> GetWorkspaceBySlugAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\", \"CreatedAt\" FROM one.\"Organizations\" WHERE \"Slug\" = @Slug LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<WorkspaceSnapshotDto>(sql, new { Slug = slug.ToLower().Trim() });
    }

    public async Task<IEnumerable<WorkspaceSnapshotDto>> GetWorkspacesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Name\", \"Slug\", \"IsActive\", \"CreatedAt\" FROM one.\"Organizations\" ORDER BY \"CreatedAt\" DESC";
        return await connection.QueryAsync<WorkspaceSnapshotDto>(sql);
    }

    public async Task<Guid?> GetTenantIdBySlugAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\" FROM one.\"Organizations\" WHERE \"Slug\" = @Slug AND \"IsActive\" = true LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<Guid?>(sql, new { Slug = slug.ToLower().Trim() });
    }

    public async Task<bool> HasTenantAccessAsync(Guid globalUserId, Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT EXISTS(SELECT 1 FROM one.\"TenantMemberships\" WHERE \"GlobalUserId\" = @Uid AND \"OrganizationId\" = @OrgId)";
        return await connection.ExecuteScalarAsync<bool>(sql, new { Uid = globalUserId, OrgId = tenantId });
    }

    public async Task<string?> GetTenantRoleAsync(Guid globalUserId, Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Role\" FROM one.\"TenantMemberships\" WHERE \"GlobalUserId\" = @Uid AND \"OrganizationId\" = @OrgId LIMIT 1";
        return await connection.ExecuteScalarAsync<string?>(sql, new { Uid = globalUserId, OrgId = tenantId });
    }

    public async Task<IEnumerable<string>> GetWorkspaceAppsAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"AppId\" FROM one.\"TenantAppEntitlements\" WHERE \"OrganizationId\" = @OrgId AND \"IsActive\" = true";
        return await connection.QueryAsync<string>(sql, new { OrgId = tenantId });
    }

    public async Task<IEnumerable<WorkspaceMemberSnapshotDto>> GetWorkspaceMembersAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT m.""Id"", m.""GlobalUserId"", u.""Name"", u.""Email"", m.""Role"", m.""CreatedAt"" as JoinedAt 
            FROM one.""TenantMemberships"" m
            JOIN one.""GlobalUsers"" u ON m.""GlobalUserId"" = u.""Id""
            WHERE m.""OrganizationId"" = @OrgId ORDER BY m.""CreatedAt"" ASC";
        return await connection.QueryAsync<WorkspaceMemberSnapshotDto>(sql, new { OrgId = tenantId });
    }

    public async Task<IEnumerable<WorkspaceInvitationSnapshotDto>> GetWorkspaceInvitationsAsync(Guid tenantId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = "SELECT \"Id\", \"Email\", \"Role\", \"Status\", \"ExpiresAt\" FROM one.\"WorkspaceInvitations\" WHERE \"OrganizationId\" = @OrgId ORDER BY \"CreatedAt\" DESC";
        return await connection.QueryAsync<WorkspaceInvitationSnapshotDto>(sql, new { OrgId = tenantId });
    }

    public async Task<IEnumerable<AppAccessRequestDto>> GetAppAccessRequestsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT r.""Id"", r.""GlobalUserId"", u.""Name"", u.""Email"", r.""RequestedApps""::text, r.""Status"", r.""CreatedAt""
            FROM one.""AppAccessRequests"" r
            JOIN one.""GlobalUsers"" u ON r.""GlobalUserId"" = u.""Id""
            ORDER BY r.""CreatedAt"" DESC";

        var rows = await connection.QueryAsync<dynamic>(sql);

        return rows.Select(r => new AppAccessRequestDto
        {
            Id = r.Id.ToString(),
            Global_user_id = r.GlobalUserId.ToString(),
            Name = r.Name,
            Email = r.Email,
            Requested_apps = JsonSerializer.Deserialize<List<string>>(r.RequestedApps) ?? new List<string>(),
            Status = r.Status,
            Created_at = new DateTimeOffset(r.CreatedAt)
        });
    }
}
