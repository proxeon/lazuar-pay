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

    public Task<bool> IsSuppressedAsync(Guid organizationId, string email) =>
        IsSuppressedAsync(organizationId, email, SuppressionLane.Marketing);

    public async Task<bool> IsSuppressedAsync(Guid organizationId, string email, SuppressionLane lane)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLowerInvariant();
        var reasons = await _dbContext.SuppressionEntries
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == organizationId && s.Email == normalized)
            .Select(s => s.Reason)
            .ToListAsync();

        if (reasons.Count == 0) return false;

        foreach (var reason in reasons)
        {
            if (reason is "BOUNCE" or "COMPLAINT" or "ANONYMIZED")
            {
                return true;
            }

            if (lane == SuppressionLane.Marketing && reason == "UNSUBSCRIBE")
            {
                return true;
            }
        }

        return false;
    }

    public async Task SuppressAsync(Guid organizationId, string email, string reason, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        var normalized = email.Trim().ToLowerInvariant();

        var existing = await _dbContext.SuppressionEntries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Email == normalized);
        if (existing is null)
        {
            _dbContext.SuppressionEntries.Add(new SuppressionEntry(organizationId, normalized, reason, source));
            await _dbContext.SaveChangesAsync();
            return;
        }

        if (existing.TryUpgrade(reason, source))
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
