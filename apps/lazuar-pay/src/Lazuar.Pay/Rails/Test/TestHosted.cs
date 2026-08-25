using Lazuar.Pay.Data;
using Lazuar.Pay.PublicPay;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Rails.Test;

public sealed class TestHosted(IConfiguration config, IHostEnvironment env) : IHostedRail
{
    public string Provider => PayProviders.Test;

    public Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        if (!PayProviders.AllowsTest(env))
        {
            throw new InvalidOperationException("rail not configured");
        }

        return Task.FromResult(new HostedSession(
            CheckoutUrls.Success(checkout, config, env),
            "test:" + checkout.Id));
    }
}
