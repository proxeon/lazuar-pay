using System;
using BuildingBlocks.Domain;
using Modules.Billing.Domain.ValueObjects;

namespace Modules.Billing.Domain.Aggregates;

public class TenantBillingProfile : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string LegalName { get; private set; }
    public string Tin { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public string? SstRegistrationNumber { get; private set; }
    public string? LogoUrl { get; private set; }
    public TenantBillingAddress? Address { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TenantBillingProfile() { }
#pragma warning restore CS8618

    public TenantBillingProfile(Guid organizationId, string legalName, string tin)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        LegalName = legalName;
        Tin = tin;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string legalName, string tin, string? registrationNumber, string? sstRegistrationNumber, string? logoUrl, TenantBillingAddress? address)
    {
        LegalName = legalName;
        Tin = tin;
        RegistrationNumber = registrationNumber;
        SstRegistrationNumber = sstRegistrationNumber;
        LogoUrl = logoUrl;
        Address = address;
        UpdatedAt = DateTime.UtcNow;
    }
}
