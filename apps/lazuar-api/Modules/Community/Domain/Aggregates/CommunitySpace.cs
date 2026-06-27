using System;
using BuildingBlocks.Domain;

namespace Modules.Community.Domain.Aggregates;

public class CommunitySpace : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; }
    public string? TelegramLink { get; private set; }
    public string? ZoomLink { get; private set; }

#pragma warning disable CS8618
    private CommunitySpace() { }
#pragma warning restore CS8618

    public CommunitySpace(Guid organizationId, Guid productId, string name, string? telegramLink, string? zoomLink)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ProductId = productId;
        Name = name;
        TelegramLink = telegramLink;
        ZoomLink = zoomLink;
    }

    public void UpdateLinks(string? telegramLink, string? zoomLink)
    {
        TelegramLink = telegramLink;
        ZoomLink = zoomLink;
    }
}
