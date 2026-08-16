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
    public string GatewayName { get; private set; }
    public CheckoutConfiguration CheckoutConfiguration { get; private set; }

    /// <summary>MyInvois tax type: 06 (N/A) or 02 (Service Tax).</summary>
    public string SstTaxType { get; private set; } = "06";

    public decimal SstRatePercent { get; private set; }

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
        string gatewayName,
        CheckoutConfiguration checkoutConfiguration,
        IEnumerable<string> fulfillmentTargets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(slug, nameof(slug));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency, nameof(currency));
        ArgumentException.ThrowIfNullOrWhiteSpace(interval, nameof(interval));
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayName, nameof(gatewayName));

        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Price = price;
        PricingModel = string.IsNullOrWhiteSpace(pricingModel) ? "FIXED" : pricingModel.Trim().ToUpperInvariant();
        MinimumPrice = minimumPrice;
        Currency = currency.Trim().ToUpperInvariant();
        Interval = interval.Trim().ToLowerInvariant();
        GatewayName = gatewayName.Trim().ToUpperInvariant();
        CheckoutConfiguration = checkoutConfiguration;
        SstTaxType = "06";
        SstRatePercent = 0m;
        IsActive = true;
        
        if (fulfillmentTargets != null)
        {
            _fulfillmentTargets.AddRange(fulfillmentTargets);
        }

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string slug, decimal price, string pricingModel, decimal minimumPrice, string interval, bool isActive, string gatewayName, CheckoutConfiguration checkoutConfiguration, IEnumerable<string> fulfillmentTargets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(slug, nameof(slug));
        ArgumentException.ThrowIfNullOrWhiteSpace(interval, nameof(interval));
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayName, nameof(gatewayName));

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Price = price;
        PricingModel = string.IsNullOrWhiteSpace(pricingModel) ? "FIXED" : pricingModel.Trim().ToUpperInvariant();
        MinimumPrice = minimumPrice;
        Interval = interval.Trim().ToLowerInvariant();
        IsActive = isActive;
        GatewayName = gatewayName.Trim().ToUpperInvariant();
        CheckoutConfiguration = checkoutConfiguration;

        if (fulfillmentTargets != null)
        {
            _fulfillmentTargets.Clear();
            _fulfillmentTargets.AddRange(fulfillmentTargets);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSst(string? taxType, decimal ratePercent)
    {
        var type = string.IsNullOrWhiteSpace(taxType) ? "06" : taxType.Trim();
        if (type is not ("06" or "02"))
        {
            throw new ArgumentException("SST tax type must be 06 or 02.", nameof(taxType));
        }

        if (type == "06" || ratePercent <= 0)
        {
            SstTaxType = "06";
            SstRatePercent = 0m;
        }
        else
        {
            SstTaxType = "02";
            SstRatePercent = ratePercent;
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
