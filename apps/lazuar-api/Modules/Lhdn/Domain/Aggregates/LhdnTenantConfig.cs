using System;
using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Aggregates;

public class LhdnTenantConfig : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public bool IntermediaryMode { get; private set; }
    
    public string SupplierTin { get; private set; }
    public string IdType { get; private set; }
    public string IdValue { get; private set; }
    public string Environment { get; private set; }
    public string? MsicCode { get; private set; }

    public string? MyInvoisClientId { get; private set; }
    public string? MyInvoisClientSecret { get; private set; }
    
    public string? EncryptedPfxBase64 { get; private set; }
    public string? PfxPasswordCiphertext { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private LhdnTenantConfig() { }
#pragma warning restore CS8618

    public LhdnTenantConfig(
        Guid organizationId, 
        bool intermediaryMode, 
        string supplierTin, 
        string idType, 
        string idValue, 
        string environment = "SANDBOX",
        string? msicCode = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        IntermediaryMode = intermediaryMode;
        SupplierTin = supplierTin.Trim().ToUpperInvariant();
        IdType = idType.Trim().ToUpperInvariant();
        IdValue = idValue.Trim();
        Environment = environment.Trim().ToUpperInvariant();
        MsicCode = msicCode;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string supplierTin, string idType, string idValue, string environment, string? msicCode, bool intermediaryMode)
    {
        SupplierTin = supplierTin.Trim().ToUpperInvariant();
        IdType = idType.Trim().ToUpperInvariant();
        IdValue = idValue.Trim();
        Environment = environment.Trim().ToUpperInvariant();
        MsicCode = msicCode;
        IntermediaryMode = intermediaryMode;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateApiCredentials(string clientId, string clientSecret)
    {
        MyInvoisClientId = clientId;
        MyInvoisClientSecret = clientSecret;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCertificate(string encryptedPfxBase64, string pfxPasswordCiphertext)
    {
        EncryptedPfxBase64 = encryptedPfxBase64;
        PfxPasswordCiphertext = pfxPasswordCiphertext;
        UpdatedAt = DateTime.UtcNow;
    }
}
