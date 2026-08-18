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

        // Match the unique key (org, email, phone). Same inbox + different phone is
        // a different buyer — do not merge tax identity onto the first TIN.
        var existingProfile = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                p => p.OrganizationId == request.OrganizationId
                     && p.Email == emailNormalized
                     && p.Phone == phoneNormalized,
                cancellationToken);

        if (existingProfile != null)
        {
            bool isModified = false;

            if (string.IsNullOrWhiteSpace(existingProfile.CompanyName) && !string.IsNullOrWhiteSpace(request.CompanyName))
            {
                existingProfile.CompanyName = request.CompanyName.Trim();
                isModified = true;
            }
            if (string.IsNullOrWhiteSpace(existingProfile.Tin) && !string.IsNullOrWhiteSpace(request.Tin))
            {
                existingProfile.Tin = request.Tin.Trim();
                isModified = true;
            }

            var incomingIdValue = string.IsNullOrWhiteSpace(request.IdValue) ? null : request.IdValue.Trim();
            var poisonedIdValue = string.IsNullOrWhiteSpace(existingProfile.IdValue)
                || (!string.IsNullOrWhiteSpace(existingProfile.CompanyName)
                    && string.Equals(existingProfile.IdValue, existingProfile.CompanyName, StringComparison.Ordinal));

            if (incomingIdValue is not null
                && !string.Equals(existingProfile.IdValue, incomingIdValue, StringComparison.Ordinal)
                && poisonedIdValue)
            {
                existingProfile.IdValue = incomingIdValue;
                isModified = true;
            }

            if (!string.IsNullOrWhiteSpace(request.IdType)
                && !string.Equals(existingProfile.IdType, request.IdType, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(existingProfile.IdType) || poisonedIdValue))
            {
                existingProfile.IdType = request.IdType;
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
            CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim(),
            Tin = string.IsNullOrWhiteSpace(request.Tin) ? null : request.Tin.Trim(),
            IdType = request.IdType,
            IdValue = request.IdValue,
            Address = address,
            ConsentedToMarketing = request.ConsentedToMarketing
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
