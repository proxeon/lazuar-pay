using Lazuar.Pay.Data;

namespace Lazuar.Pay.Rails;

public interface IHostedRail
{
    string Provider { get; }

    Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct);
}
