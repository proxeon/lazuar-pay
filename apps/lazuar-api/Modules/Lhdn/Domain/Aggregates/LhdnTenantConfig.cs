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

    /// <summary>Registered legal name used as UBL supplier/buyer party name.</summary>
    public string? LegalName { get; private set; }
    public string? AddressLine1 { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? Postal { get; private set; }
    /// <summary>ISO country code, e.g. MYS.</summary>
    public string? Country { get; private set; }
    
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

    /// <summary>
    /// Updates legal address fields. Null args leave the existing value; empty/whitespace clears.
    /// </summary>
    public void UpdateLegalAddress(
        string? legalName,
        string? addressLine1,
        string? city,
        string? state,
        string? postal,
        string? country)
    {
        if (legalName != null) LegalName = NormalizeOptional(legalName);
        if (addressLine1 != null) AddressLine1 = NormalizeOptional(addressLine1);
        if (city != null) City = NormalizeOptional(city);
        if (state != null) State = NormalizeOptional(state);
        if (postal != null) Postal = NormalizeOptional(postal);
        if (country != null)
        {
            Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant();
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateApiCredentials(string clientId, string clientSecret)
    {
        MyInvoisClientId = clientId;
        MyInvoisClientSecret = clientSecret;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Update client id always; secret only when a non-empty value is provided.</summary>
    public void UpdateApiCredentialsPreserveSecret(string? clientId, string? clientSecretOrNullToKeep)
    {
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            MyInvoisClientId = clientId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(clientSecretOrNullToKeep))
        {
            MyInvoisClientSecret = clientSecretOrNullToKeep;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCertificate(string encryptedPfxBase64, string pfxPasswordCiphertext)
    {
        EncryptedPfxBase64 = encryptedPfxBase64;
        PfxPasswordCiphertext = pfxPasswordCiphertext;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
