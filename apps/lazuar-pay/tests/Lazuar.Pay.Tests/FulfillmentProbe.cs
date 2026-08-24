using Lazuar.Pay.Money;

namespace Lazuar.Pay.Tests;

public sealed class FulfillmentProbe
{
    public bool ThrowNext { get; set; }
}

public sealed class ProbingFulfillment(Fulfillment inner, FulfillmentProbe probe) : IFulfillPaid
{
    public Task FulfillPaidAsync(string checkoutId, string provider, string? providerRef, CancellationToken ct)
    {
        if (probe.ThrowNext)
        {
            probe.ThrowNext = false;
            throw new InvalidOperationException("fulfill boom");
        }

        return inner.FulfillPaidAsync(checkoutId, provider, providerRef, ct);
    }
}
