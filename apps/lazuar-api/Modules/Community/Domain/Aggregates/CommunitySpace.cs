using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Aggregates;

public class CommunitySpace : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    
    private readonly List<Guid> _productIds = new();
    public IReadOnlyCollection<Guid> ProductIds => _productIds.AsReadOnly();

    public string Name { get; private set; }
    public string? TelegramLink { get; private set; }
    public string? ZoomLink { get; private set; }

#pragma warning disable CS8618
    private CommunitySpace() { }
#pragma warning restore CS8618

    public CommunitySpace(Guid organizationId, IEnumerable<Guid> productIds, string name, string? telegramLink, string? zoomLink)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name;
        TelegramLink = telegramLink;
        ZoomLink = zoomLink;

        if (productIds != null)
        {
            _productIds.AddRange(productIds);
        }
    }

    public void UpdateLinks(string? telegramLink, string? zoomLink)
    {
        TelegramLink = telegramLink;
        ZoomLink = zoomLink;
    }
}
