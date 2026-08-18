using System.Collections.Generic;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.ValueObjects;

public class AdHocLineItem : ValueObject
{
    public string Description { get; }
    public int Quantity { get; }
    public decimal UnitPrice { get; }

    public AdHocLineItem(string description, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Line description is required.", nameof(description));
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Line quantity must be at least 1.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Line unit price cannot be negative.");
        }

        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Description;
        yield return Quantity;
        yield return UnitPrice;
    }
}
