using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Contracts;

namespace Modules.CRM.Infrastructure;

public class CrmQueryService : ICrmQueryService
{
    private readonly CrmDbContext _dbContext;

    public CrmQueryService(CrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid profileId)
    {
        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == profileId);

        if (profile == null) return null;

        return new ClientProfileDto(profile.Id, profile.FullName, profile.Email, profile.Phone);
    }

    public async Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(IEnumerable<Guid> profileIds)
    {
        var ids = profileIds.Distinct().ToList();
        if (ids.Count == 0) return Enumerable.Empty<ClientProfileDto>();

        var profiles = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        return profiles.Select(p => new ClientProfileDto(p.Id, p.FullName, p.Email, p.Phone));
    }

    public async Task<ClientProfileDto?> GetClientProfileByEmailAsync(Guid organizationId, string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Email == normalizedEmail);

        if (profile == null) return null;

        return new ClientProfileDto(profile.Id, profile.FullName, profile.Email, profile.Phone);
    }
}
