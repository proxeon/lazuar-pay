using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Contracts;
using Modules.CRM.Domain;

namespace Modules.CRM.Infrastructure;

public class CrmQueryService : ICrmQueryService
{
    private readonly CrmDbContext _dbContext;

    public CrmQueryService(CrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private ClientProfileDto MapToDto(ClientProfileEntity entity)
    {
        BillingAddressDto? addressDto = null;
        if (entity.Address != null)
        {
            addressDto = new BillingAddressDto
            {
                Line1 = entity.Address.Line1,
                Line2 = entity.Address.Line2,
                Line3 = entity.Address.Line3,
                City = entity.Address.City,
                Postal_code = entity.Address.PostalCode,
                State_code = entity.Address.StateCode,
                Country_code = entity.Address.CountryCode
            };
        }

        return new ClientProfileDto
        {
            Id = entity.Id.ToString(),
            Full_name = entity.FullName,
            Email = entity.Email,
            Phone = entity.Phone,
            Company_name = entity.CompanyName,
            Global_user_id = entity.GlobalUserId?.ToString(),
            Tin = entity.Tin,
            Id_type = entity.IdType,
            Id_value = entity.IdValue,
            Billing_address = addressDto,
            Consented_to_marketing = entity.ConsentedToMarketing
        };
    }

    public async Task<ClientProfileDto?> GetClientProfileAsync(Guid organizationId, Guid profileId)
    {
        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Id == profileId);

        return profile == null ? null : MapToDto(profile);
    }

    public async Task<IEnumerable<ClientProfileDto>> GetClientProfilesAsync(Guid organizationId, IEnumerable<Guid> profileIds)
    {
        var ids = profileIds.Distinct().ToList();
        if (ids.Count == 0) return Enumerable.Empty<ClientProfileDto>();

        var profiles = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == organizationId && ids.Contains(p.Id))
            .ToListAsync();

        return profiles.Select(MapToDto);
    }

    public async Task<ClientProfileDto?> GetClientProfileByEmailAsync(Guid organizationId, string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Email == normalizedEmail);

        return profile == null ? null : MapToDto(profile);
    }
}
