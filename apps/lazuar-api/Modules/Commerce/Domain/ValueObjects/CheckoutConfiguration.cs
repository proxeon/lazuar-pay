using System.Collections.Generic;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.ValueObjects;

public class CheckoutConfiguration : ValueObject
{
    public bool RequiresAddress { get; }
    public bool RequiresTaxId { get; }
    public bool RequiresPhone { get; }

    public CheckoutConfiguration(bool requiresAddress, bool requiresTaxId, bool requiresPhone)
    {
        RequiresAddress = requiresAddress;
        RequiresTaxId = requiresTaxId;
        RequiresPhone = requiresPhone;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RequiresAddress;
        yield return RequiresTaxId;
        yield return RequiresPhone;
    }
}
