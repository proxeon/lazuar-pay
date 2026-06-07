using BuildingBlocks.Application;

namespace Modules.One.Contracts;

public record WorkspaceSnapshotDto(Guid Id, string Name, string Slug, bool IsActive, DateTime CreatedAt);

public interface IOneQueryService
{
    Task<WorkspaceSnapshotDto?> GetWorkspaceByIdAsync(Guid tenantId);
    Task<WorkspaceSnapshotDto?> GetWorkspaceBySlugAsync(string slug);
    Task<IEnumerable<WorkspaceSnapshotDto>> GetWorkspacesAsync();
    
    Task<Guid?> GetTenantIdBySlugAsync(string slug);
    Task<bool> HasTenantAccessAsync(Guid globalUserId, Guid tenantId);
    Task<string?> GetTenantRoleAsync(Guid globalUserId, Guid tenantId);
    Task<IEnumerable<string>> GetWorkspaceAppsAsync(Guid tenantId);
}
