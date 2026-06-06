using BuildingBlocks.Application;

namespace Modules.One.Contracts;

public record WorkspaceSnapshotDto(Guid Id, string Name, string Slug, bool IsActive);

public interface IOneQueryService
{
    Task<WorkspaceSnapshotDto?> GetWorkspaceByIdAsync(Guid tenantId);
    Task<WorkspaceSnapshotDto?> GetWorkspaceBySlugAsync(string slug);
}
