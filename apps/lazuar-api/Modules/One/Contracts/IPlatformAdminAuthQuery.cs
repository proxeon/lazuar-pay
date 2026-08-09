// apps/lazuar-api/Modules/One/Contracts/IPlatformAdminAuthQuery.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.One.Contracts;

/// <summary>
/// Login projection for platform super-admin auth (includes password hash).
/// Only returned for users with <c>IsSystemAdmin</c>.
/// </summary>
public record PlatformAdminLoginUserDto(
    Guid Id,
    string Email,
    string Name,
    string PasswordHash,
    Guid SecurityStamp,
    bool IsSystemAdmin,
    bool IsEmailVerified,
    bool IsActive);

/// <summary>
/// Identity projection for platform super-admin /auth/me (no password hash).
/// Only returned for users with <c>IsSystemAdmin</c>.
/// </summary>
public record PlatformAdminUserDto(
    Guid Id,
    string Email,
    string Name,
    Guid SecurityStamp,
    bool IsSystemAdmin,
    bool IsEmailVerified,
    bool IsActive);

/// <summary>
/// One-owned read port for platform super-admin cookie auth under <c>/api/v1/platform</c>.
/// Consumers must not query <c>one.GlobalUsers</c> directly.
/// </summary>
public interface IPlatformAdminAuthQuery
{
    /// <summary>
    /// Resolve a system admin by email (normalized lower-case). Includes password hash for verify.
    /// Returns null when no matching system admin exists.
    /// </summary>
    Task<PlatformAdminLoginUserDto?> GetSystemAdminByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a system admin by id for session revalidation. Does not include password hash.
    /// Returns null when no matching system admin exists.
    /// </summary>
    Task<PlatformAdminUserDto?> GetSystemAdminByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
