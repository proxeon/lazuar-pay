using System;
using System.Collections.Generic;
using System.Linq;
using BuildingBlocks.Domain;
using Modules.Commerce.Domain.Entities;
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

    public int TrialDays { get; private set; }

    private readonly List<string> _fulfillmentTargets = new();
    public IReadOnlyCollection<string> FulfillmentTargets => _fulfillmentTargets.AsReadOnly();

    private readonly List<ProductPrice> _prices = new();
    public IReadOnlyCollection<ProductPrice> Prices => _prices.AsReadOnly();

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
        TrialDays = 0;
        IsActive = true;
        SyncDefaultPrice();
        
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
        SyncDefaultPrice();
    }

    public void SetTrialDays(int days)
    {
        if (days < 0 || days > 90)
        {
            throw new InvalidOperationException("Trial days must be between 0 and 90.");
        }

        if (days > 0 && string.Equals(Interval, "one_time", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Free trial is not available on one-time products.");
        }

        TrialDays = days;
        UpdatedAt = DateTime.UtcNow;
    }

    public ProductPrice? GetPrice(string interval)
    {
        var normalized = (interval ?? string.Empty).Trim().ToLowerInvariant();
        return _prices.FirstOrDefault(p => p.Interval == normalized);
    }

    public ProductPrice? DefaultPrice() =>
        _prices.FirstOrDefault(p => p.IsDefault) ?? _prices.FirstOrDefault(p => p.Interval == Interval);

    public void UpsertPrice(string interval, decimal amount, bool isDefault)
    {
        var normalized = ProductPrice.NormalizeInterval(interval);
        if (normalized is not (ProductPrice.IntervalMonth or ProductPrice.IntervalYear or "one_time"))
        {
            throw new InvalidOperationException("Only monthly, yearly, or one-time prices are supported.");
        }

        if (_prices.Count(p => p.Interval != normalized) >= 2 && GetPrice(normalized) == null)
        {
            throw new InvalidOperationException("A product can have at most monthly and yearly prices.");
        }

        var existing = GetPrice(normalized);
        if (existing == null)
        {
            if (_prices.Select(p => p.Interval).Distinct().Count() >= 2)
            {
                throw new InvalidOperationException("A product can have at most monthly and yearly prices.");
            }

            _prices.Add(ProductPrice.Create(Id, normalized, amount, isDefault));
        }
        else
        {
            existing.Update(normalized, amount, isDefault || existing.IsDefault);
        }

        if (isDefault)
        {
            foreach (var price in _prices.Where(p => p.Interval != normalized))
            {
                price.ClearDefault();
            }
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetYearlyPrice(decimal? amount)
    {
        if (amount == null)
        {
            return;
        }

        if (string.Equals(Interval, "one_time", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Yearly price is only available on recurring products.");
        }

        if (amount.Value < 0)
        {
            throw new InvalidOperationException("Yearly price cannot be negative.");
        }

        UpsertPrice(ProductPrice.IntervalYear, amount.Value, isDefault: Interval == ProductPrice.IntervalYear);
    }

    internal void SyncDefaultPrice()
    {
        var existingDefault = _prices.FirstOrDefault(p => p.IsDefault)
            ?? _prices.FirstOrDefault(p => p.Interval == Interval);
        if (existingDefault == null)
        {
            _prices.Add(ProductPrice.Create(Id, Interval, Price, isDefault: true));
            return;
        }

        existingDefault.Update(Interval, Price, isDefault: true);
        foreach (var other in _prices.Where(p => p.Id != existingDefault.Id))
        {
            other.ClearDefault();
        }
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
