using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Hosting;

internal static class PayBoot
{
    public static void ThrowIfMisconfigured(IConfiguration config, IHostEnvironment env)
    {
        if (env.IsDevelopment() || env.IsEnvironment("Testing"))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(config["Pay:WrapKey"]))
        {
            throw new InvalidOperationException("Pay:WrapKey is required");
        }

        if (string.IsNullOrWhiteSpace(config.GetConnectionString("Pay")))
        {
            throw new InvalidOperationException("ConnectionStrings:Pay is required");
        }

        var one = config["One:BaseUrl"]?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(one)
            || one.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || one.Contains("127.0.0.1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("One:BaseUrl must be a public URL in Production and Staging");
        }

        Identity.Client.OneWorkerClient.ThrowIfInvalid(config);
    }
}
