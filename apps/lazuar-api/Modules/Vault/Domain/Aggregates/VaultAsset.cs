using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;

namespace Modules.Vault.Domain.Aggregates;

public class VaultAsset : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    
    private readonly List<Guid> _productIds = new();
    public IReadOnlyCollection<Guid> ProductIds => _productIds.AsReadOnly();

    public string Name { get; private set; }
    public string CloudflareR2Url { get; private set; }

#pragma warning disable CS8618
    private VaultAsset() { }
#pragma warning restore CS8618

    public VaultAsset(Guid organizationId, IEnumerable<Guid> productIds, string name, string cloudflareR2Url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(cloudflareR2Url, nameof(cloudflareR2Url));

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name.Trim();
        CloudflareR2Url = cloudflareR2Url.Trim();

        if (productIds != null)
        {
            _productIds.AddRange(productIds);
        }
    }

    public void UpdateDetails(string name, string cloudflareR2Url, IEnumerable<Guid> productIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(cloudflareR2Url, nameof(cloudflareR2Url));

        Name = name.Trim();
        CloudflareR2Url = cloudflareR2Url.Trim();

        _productIds.Clear();
        if (productIds != null)
        {
            _productIds.AddRange(productIds);
        }
    }
}
