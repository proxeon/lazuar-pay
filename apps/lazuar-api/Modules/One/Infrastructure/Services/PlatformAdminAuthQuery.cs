// apps/lazuar-api/Modules/One/Infrastructure/Services/PlatformAdminAuthQuery.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.One.Contracts;

namespace Modules.One.Infrastructure.Services;

/// <summary>
/// EF-backed implementation of platform super-admin reads against <c>one.GlobalUsers</c> only.
/// </summary>
public sealed class PlatformAdminAuthQuery : IPlatformAdminAuthQuery
{
    private readonly OneDbContext _context;

    public PlatformAdminAuthQuery(OneDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformAdminLoginUserDto?> GetSystemAdminByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();

        return await _context.GlobalUsers
            .AsNoTracking()
            .Where(u => u.Email == normalized && u.IsSystemAdmin)
            .Select(u => new PlatformAdminLoginUserDto(
                u.Id,
                u.Email,
                u.Name,
                u.PasswordHash,
                u.SecurityStamp,
                u.IsSystemAdmin,
                u.IsEmailVerified,
                u.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PlatformAdminUserDto?> GetSystemAdminByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.GlobalUsers
            .AsNoTracking()
            .Where(u => u.Id == id && u.IsSystemAdmin)
            .Select(u => new PlatformAdminUserDto(
                u.Id,
                u.Email,
                u.Name,
                u.SecurityStamp,
                u.IsSystemAdmin,
                u.IsEmailVerified,
                u.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
