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
        Description = description;
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
