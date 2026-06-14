using System;
using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Entities;

public class TinValidateCache : Entity, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Tin { get; private set; }
    public string IdType { get; private set; }
    public string IdValueHash { get; private set; }
    public bool IsValid { get; private set; }
    public string? TaxpayerName { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

#pragma warning disable CS8618
    private TinValidateCache() { }
#pragma warning restore CS8618

    public TinValidateCache(Guid organizationId, string tin, string idType, string idValueHash, bool isValid, string? taxpayerName, DateTime expiresAt)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Tin = tin.Trim().ToUpperInvariant();
        IdType = idType.Trim().ToUpperInvariant();
        IdValueHash = idValueHash;
        IsValid = isValid;
        TaxpayerName = taxpayerName;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateResult(bool isValid, string? taxpayerName, DateTime expiresAt)
    {
        IsValid = isValid;
        TaxpayerName = taxpayerName;
        ExpiresAt = expiresAt;
    }
}
