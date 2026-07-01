using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Communications.Contracts;
using Modules.Communications.Domain.Aggregates;

namespace Modules.Communications.Infrastructure.Services;

public class SuppressionService : ISuppressionService
{
    private readonly CommunicationsDbContext _dbContext;

    public SuppressionService(CommunicationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsSuppressedAsync(Guid organizationId, string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLowerInvariant();
        return await _dbContext.SuppressionEntries
            .AnyAsync(s => s.OrganizationId == organizationId && s.Email == normalized);
    }

    public async Task SuppressAsync(Guid organizationId, string email, string reason, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var normalized = email.Trim().ToLowerInvariant();

        var exists = await _dbContext.SuppressionEntries
            .AnyAsync(s => s.OrganizationId == organizationId && s.Email == normalized);
        if (exists) return;

        _dbContext.SuppressionEntries.Add(new SuppressionEntry(organizationId, normalized, reason, source));
        await _dbContext.SaveChangesAsync();
    }
}
