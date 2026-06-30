using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Domain.ValueObjects;

namespace Modules.Billing.Infrastructure.Commands;

public class UpdateTenantBillingProfileCommandHandler : ICommandHandler<UpdateTenantBillingProfileCommand>
{
    private readonly BillingDbContext _dbContext;

    public UpdateTenantBillingProfileCommandHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpdateTenantBillingProfileCommand request, CancellationToken ct)
    {
        var profile = await _dbContext.TenantBillingProfiles
            .FirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId, ct);

        TenantBillingAddress? address = null;
        if (request.Address != null)
        {
            address = new TenantBillingAddress(
                request.Address.Line1,
                request.Address.Line2,
                request.Address.Line3,
                request.Address.City,
                request.Address.Postal_code,
                request.Address.State_code,
                request.Address.Country_code
            );
        }

        if (profile == null)
        {
            profile = new TenantBillingProfile(request.OrganizationId, request.LegalName, request.Tin);
            profile.UpdateProfile(request.LegalName, request.Tin, request.RegistrationNumber, request.SstRegistrationNumber, request.LogoUrl, address);
            _dbContext.TenantBillingProfiles.Add(profile);
        }
        else
        {
            profile.UpdateProfile(request.LegalName, request.Tin, request.RegistrationNumber, request.SstRegistrationNumber, request.LogoUrl, address);
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
