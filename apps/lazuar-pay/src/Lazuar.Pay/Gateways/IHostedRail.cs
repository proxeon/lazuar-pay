using Lazuar.Pay.Data;

namespace Lazuar.Pay.Gateways;

public readonly record struct HostedSession(string RedirectUrl, string? ProviderSessionId);

public interface IHostedRail
{
    string Provider { get; }

    Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct);
}
