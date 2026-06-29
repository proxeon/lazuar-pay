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
    public string PricingModel { get; private set; }
    public decimal MinimumPrice { get; private set; }
    public string Currency { get; private set; }
    public string Interval { get; private set; }
    public bool IsActive { get; private set; }
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
        string pricingModel,
        decimal minimumPrice,
        string currency, 
        string interval,
        CheckoutConfiguration checkoutConfiguration,
        IEnumerable<string> fulfillmentTargets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(slug, nameof(slug));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency, nameof(currency));
        ArgumentException.ThrowIfNullOrWhiteSpace(interval, nameof(interval));

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Price = price;
        PricingModel = string.IsNullOrWhiteSpace(pricingModel) ? "FIXED" : pricingModel.Trim().ToUpperInvariant();
        MinimumPrice = minimumPrice;
        Currency = currency.Trim().ToUpperInvariant();
        Interval = interval.Trim().ToLowerInvariant();
        CheckoutConfiguration = checkoutConfiguration;
        IsActive = true;
        
        if (fulfillmentTargets != null)
        {
            _fulfillmentTargets.AddRange(fulfillmentTargets);
        }

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string slug, decimal price, string pricingModel, decimal minimumPrice, string interval, bool isActive, CheckoutConfiguration checkoutConfiguration, IEnumerable<string> fulfillmentTargets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(slug, nameof(slug));
        ArgumentException.ThrowIfNullOrWhiteSpace(interval, nameof(interval));

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Price = price;
        PricingModel = string.IsNullOrWhiteSpace(pricingModel) ? "FIXED" : pricingModel.Trim().ToUpperInvariant();
        MinimumPrice = minimumPrice;
        Interval = interval.Trim().ToLowerInvariant();
        IsActive = isActive;
        CheckoutConfiguration = checkoutConfiguration;

        if (fulfillmentTargets != null)
        {
            _fulfillmentTargets.Clear();
            _fulfillmentTargets.AddRange(fulfillmentTargets);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
