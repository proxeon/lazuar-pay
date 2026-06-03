using BuildingBlocks.Application;

namespace Modules.Tenant.Contracts;

public record TenantSnapshotDto(Guid Id, string Name, string Slug, bool IsActive);

public interface ITenantQueryService
{
    Task<TenantSnapshotDto?> GetTenantByIdAsync(Guid tenantId);
    Task<TenantSnapshotDto?> GetTenantBySlugAsync(string slug);
}
