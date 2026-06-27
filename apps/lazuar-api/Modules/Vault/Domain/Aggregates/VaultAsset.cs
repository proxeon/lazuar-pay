using System;
using BuildingBlocks.Domain;

namespace Modules.Vault.Domain.Aggregates;

public class VaultAsset : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; }
    public string CloudflareR2Url { get; private set; }

#pragma warning disable CS8618
    private VaultAsset() { }
#pragma warning restore CS8618

    public VaultAsset(Guid organizationId, Guid productId, string name, string cloudflareR2Url)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ProductId = productId;
        Name = name;
        CloudflareR2Url = cloudflareR2Url;
    }
}
