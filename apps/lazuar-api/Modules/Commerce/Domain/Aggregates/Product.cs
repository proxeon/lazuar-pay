using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;
using Modules.Commerce.Domain.ValueObjects;

namespace Modules.Commerce.Domain.Aggregates;

public class Product : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; }
    public string Interval { get; private set; }
    public CheckoutConfiguration CheckoutConfiguration { get; private set; }

    private readonly List<string> _fulfillmentTargets = new();
    public IReadOnlyCollection<string> FulfillmentTargets => _fulfillmentTargets.AsReadOnly();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private Product() { }
#pragma warning restore CS8618

    public Product(
        Guid organizationId, 
        string name, 
        string slug, 
        decimal price, 
        string currency, 
        string interval,
        CheckoutConfiguration checkoutConfiguration,
        IEnumerable<string> fulfillmentTargets)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Price = price;
        Currency = currency.ToUpperInvariant();
        Interval = interval.ToLowerInvariant();
        CheckoutConfiguration = checkoutConfiguration;
        
        if (fulfillmentTargets != null)
        {
            _fulfillmentTargets.AddRange(fulfillmentTargets);
        }

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string slug, decimal price, string interval, CheckoutConfiguration checkoutConfiguration, IEnumerable<string> fulfillmentTargets)
    {
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Price = price;
        Interval = interval.ToLowerInvariant();
        CheckoutConfiguration = checkoutConfiguration;

        _fulfillmentTargets.Clear();
        if (fulfillmentTargets != null)
        {
            _fulfillmentTargets.AddRange(fulfillmentTargets);
        }

        UpdatedAt = DateTime.UtcNow;
    }
}
