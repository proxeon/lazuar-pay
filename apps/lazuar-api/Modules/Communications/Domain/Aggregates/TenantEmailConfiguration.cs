using System;
using BuildingBlocks.Domain;

namespace Modules.Communications.Domain.Aggregates;

public class TenantEmailConfiguration : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }

    /// <summary>AES-encrypted Resend API key (base64 IV+ciphertext). Never return raw to clients.</summary>
    public string ApiKey { get; private set; }

    public string SenderEmail { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private TenantEmailConfiguration() { }
#pragma warning restore CS8618

    public TenantEmailConfiguration(Guid organizationId, string encryptedApiKey, string senderEmail, bool isActive)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ApiKey = encryptedApiKey.Trim();
        SenderEmail = senderEmail.Trim().ToLowerInvariant();
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateConfiguration(string encryptedApiKey, string senderEmail, bool isActive)
    {
        ApiKey = encryptedApiKey.Trim();
        SenderEmail = senderEmail.Trim().ToLowerInvariant();
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Update sender/active without rotating the encrypted key.</summary>
    public void UpdateWithoutKey(string senderEmail, bool isActive)
    {
        SenderEmail = senderEmail.Trim().ToLowerInvariant();
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
