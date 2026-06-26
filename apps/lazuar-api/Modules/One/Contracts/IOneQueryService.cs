// apps/lazuar-api/Modules/One/Contracts/IOneQueryService.cs
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.One.Contracts;

public record WorkspaceSnapshotDto(Guid Id, string Name, string Slug, bool IsActive, DateTime CreatedAt);
public record WorkspaceMemberSnapshotDto(Guid Id, Guid GlobalUserId, string Name, string Email, string Role, DateTime JoinedAt);
public record WorkspaceInvitationSnapshotDto(Guid Id, string Email, string Role, string Status, DateTime ExpiresAt);
public record WorkspaceEntitlementSnapshotDto(string AppId, bool IsActive);
public record WebhookEndpointSnapshotDto(Guid Id, string Url, string SecretKey, bool IsActive, DateTime CreatedAt);
public record WebhookDeliveryLogSnapshotDto(Guid Id, string EventType, string Status, int AttemptCount, string? LastError, DateTime CreatedAt);

public interface IOneQueryService
{
    Task<WorkspaceSnapshotDto?> GetWorkspaceByIdAsync(Guid tenantId);
    Task<WorkspaceSnapshotDto?> GetWorkspaceBySlugAsync(string slug);
    Task<IEnumerable<WorkspaceSnapshotDto>> GetWorkspacesAsync();
    Task<Guid?> GetTenantIdBySlugAsync(string slug);
    Task<bool> HasTenantAccessAsync(Guid globalUserId, Guid tenantId);
    Task<string?> GetTenantRoleAsync(Guid globalUserId, Guid tenantId);
    Task<IEnumerable<string>> GetWorkspaceAppsAsync(Guid tenantId);
    Task<IEnumerable<WorkspaceEntitlementSnapshotDto>> GetWorkspaceEntitlementsAsync(Guid tenantId);
    Task<IEnumerable<WorkspaceMemberSnapshotDto>> GetWorkspaceMembersAsync(Guid tenantId);
    Task<IEnumerable<WorkspaceInvitationSnapshotDto>> GetWorkspaceInvitationsAsync(Guid tenantId);
    Task<WebhookEndpointSnapshotDto?> GetWorkspaceWebhookAsync(Guid tenantId);
    Task<IEnumerable<WebhookDeliveryLogSnapshotDto>> GetWorkspaceWebhookLogsAsync(Guid tenantId);
}
