using System;
using BuildingBlocks.Domain;

namespace Modules.Lhdn.Domain.Aggregates;

public class LhdnTenantConfig : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public bool IntermediaryMode { get; private set; }
    
    public string? EncryptedPfxBase64 { get; private set; }
    public string? PfxPasswordCiphertext { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private LhdnTenantConfig() { }
#pragma warning restore CS8618

    public LhdnTenantConfig(Guid organizationId, bool intermediaryMode)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        IntermediaryMode = intermediaryMode;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCertificate(string encryptedPfxBase64, string pfxPasswordCiphertext)
    {
        EncryptedPfxBase64 = encryptedPfxBase64;
        PfxPasswordCiphertext = pfxPasswordCiphertext;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetIntermediaryMode(bool isIntermediary)
    {
        IntermediaryMode = isIntermediary;
        UpdatedAt = DateTime.UtcNow;
    }
}
