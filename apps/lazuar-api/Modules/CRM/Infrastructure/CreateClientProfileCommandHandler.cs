using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Contracts;
using Modules.CRM.Domain;

namespace Modules.CRM.Infrastructure;

public class CreateClientProfileCommandHandler : ICommandHandler<CreateClientProfileCommand, Guid>
{
    private readonly CrmDbContext _dbContext;

    public CreateClientProfileCommandHandler(CrmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(CreateClientProfileCommand request, CancellationToken cancellationToken)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var phoneNormalized = NormalizePhone(request.Phone);

        var existingProfile = await _dbContext.ClientProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId
                && (p.Email == emailNormalized || p.Phone == phoneNormalized), cancellationToken);

        if (existingProfile != null)
        {
            if (existingProfile.GlobalUserId == null && request.GlobalUserId.HasValue)
            {
                existingProfile.GlobalUserId = request.GlobalUserId.Value;
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
            GlobalUserId = request.GlobalUserId,
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
