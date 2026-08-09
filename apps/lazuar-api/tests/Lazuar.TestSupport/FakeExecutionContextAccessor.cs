using BuildingBlocks.Application;

namespace Lazuar.TestSupport;

/// <summary>
/// Mutable stand-in for <see cref="IExecutionContextAccessor"/> in unit/module tests.
/// Prefer this over NSubstitute when only tenant/user ambient values are needed.
/// </summary>
public sealed class FakeExecutionContextAccessor : IExecutionContextAccessor
{
    public Guid TenantId { get; set; } = Guid.Empty;
    public Guid UserId { get; set; } = Guid.Empty;
    public string UserRole { get; set; } = "OrgAdmin";
    public bool IsSystemAdmin { get; set; }
    public bool IsTestMode { get; set; }
    public string AuditSignature { get; set; } = "test";

    /// <summary>
    /// Empty tenant — matches most InMemory DbContext fixtures that seed with explicit OrganizationId
    /// and rely on fail-closed query filters (empty ambient tenant matches no rows until IgnoreQueryFilters).
    /// </summary>
    public static FakeExecutionContextAccessor EmptyTenant() => new();

    public static FakeExecutionContextAccessor ForTenant(Guid tenantId, Guid? userId = null) =>
        new()
        {
            TenantId = tenantId,
            UserId = userId ?? Guid.Empty
        };
}
