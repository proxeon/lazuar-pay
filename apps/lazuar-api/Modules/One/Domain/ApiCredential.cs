using System;
using BuildingBlocks.Domain;

namespace Modules.One.Domain;

/// <summary>
/// Platform API credential (machine client key) owned by One.
/// Mirrors the former LHDN-local <c>DeveloperApiKey</c> shape plus optional creator.
/// </summary>
public class ApiCredential : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; private set; }
    public string Prefix { get; private set; }
    public string KeyHash { get; private set; }
    /// <summary>Last 4 characters of the plain key for list UI (never the secret).</summary>
    public string KeyHint { get; private set; }
    /// <summary>Space-separated OAuth-style scopes (e.g. lhdn.documents:write).</summary>
    public string Scopes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    /// <summary>Optional human who minted the key (null for system/legacy).</summary>
    public Guid? CreatedByUserId { get; private set; }

#pragma warning disable CS8618
    private ApiCredential() { }
#pragma warning restore CS8618

    public ApiCredential(
        Guid organizationId,
        string name,
        string prefix,
        string keyHash,
        string keyHint,
        string scopes,
        Guid? createdByUserId = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name;
        Prefix = prefix;
        KeyHash = keyHash;
        KeyHint = keyHint;
        Scopes = scopes;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }

    public void Revoke()
    {
        IsActive = false;
    }
}
