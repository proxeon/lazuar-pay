using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Contracts;
using Modules.CRM.Domain;

namespace Modules.CRM.Infrastructure;

public class ResolveClientProfileCommandHandler : ICommandHandler<ResolveClientProfileCommand, Guid>
{
    private readonly CrmDbContext _dbContext;

    public ResolveClientProfileCommandHandler(CrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(ResolveClientProfileCommand request, CancellationToken cancellationToken)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var phoneNormalized = NormalizePhone(request.Phone);

        // Retrieve existing profile across global filters using tenant isolation bypass
        var existingProfile = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId && p.Email == emailNormalized, cancellationToken);

        if (existingProfile != null)
        {
            bool isModified = false;

            // Enrich existing profile with richer data provided during checkout
            if (string.IsNullOrWhiteSpace(existingProfile.Phone) && !string.IsNullOrWhiteSpace(phoneNormalized))
            {
                existingProfile.Phone = phoneNormalized;
                isModified = true;
            }
            if (string.IsNullOrWhiteSpace(existingProfile.Tin) && !string.IsNullOrWhiteSpace(request.Tin))
            {
                existingProfile.Tin = request.Tin;
                isModified = true;
            }
            if (string.IsNullOrWhiteSpace(existingProfile.IdType) && !string.IsNullOrWhiteSpace(request.IdType))
            {
                existingProfile.IdType = request.IdType;
                isModified = true;
            }
            if (string.IsNullOrWhiteSpace(existingProfile.IdValue) && !string.IsNullOrWhiteSpace(request.IdValue))
            {
                existingProfile.IdValue = request.IdValue;
                isModified = true;
            }
            if (existingProfile.Address == null && request.BillingAddress != null)
            {
                existingProfile.Address = new BillingAddress(
                    request.BillingAddress.Line1,
                    request.BillingAddress.Line2,
                    request.BillingAddress.Line3,
                    request.BillingAddress.City,
                    request.BillingAddress.Postal_code,
                    request.BillingAddress.State_code,
                    request.BillingAddress.Country_code
                );
                isModified = true;
            }

            if (isModified)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return existingProfile.Id;
        }

        BillingAddress? address = null;
        if (request.BillingAddress != null)
        {
            address = new BillingAddress(
                request.BillingAddress.Line1,
                request.BillingAddress.Line2,
                request.BillingAddress.Line3,
                request.BillingAddress.City,
                request.BillingAddress.Postal_code,
                request.BillingAddress.State_code,
                request.BillingAddress.Country_code
            );
        }

        var profile = new ClientProfileEntity
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = request.OrganizationId,
            FullName = request.FullName.Trim(),
            Email = emailNormalized,
            Phone = phoneNormalized,
            Tin = request.Tin,
            IdType = request.IdType,
            IdValue = request.IdValue,
            Address = address,
            ConsentedToMarketing = true
        };

        await _dbContext.ClientProfiles.AddAsync(profile, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return profile.Id;
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";

        var normalized = phone
            .Replace("+", "")
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "");

        if (normalized.StartsWith('0'))
        {
            normalized = "60" + normalized[1..];
        }

        return normalized;
    }
}
