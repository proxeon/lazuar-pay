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

        var wrap = config["Pay:WrapKey"]?.Trim() ?? "";
        try
        {
            var key = Convert.FromBase64String(wrap);
            if (key.Length != 32)
            {
                throw new InvalidOperationException("Pay:WrapKey must be 32 bytes base64");
            }
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Pay:WrapKey must be 32 bytes base64");
        }

        var checkoutBase = config["Pay:CheckoutBaseUrl"]?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(checkoutBase)
            || !Uri.TryCreate(checkoutBase, UriKind.Absolute, out var checkoutUri)
            || checkoutUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Pay:CheckoutBaseUrl must be public https in Production and Staging");
        }

        var startMax = config.GetValue("Pay:StartMaxPerMinute", 20);
        if (startMax <= 0)
        {
            throw new InvalidOperationException("Pay:StartMaxPerMinute must be greater than 0");
        }

        var cluster = config["Pay:Solana:Cluster"]?.Trim() ?? "";
        if (cluster is not ("mainnet-beta" or "devnet"))
        {
            throw new InvalidOperationException("Pay:Solana:Cluster must be mainnet-beta or devnet");
        }

        if (env.IsProduction() && cluster != "mainnet-beta")
        {
            throw new InvalidOperationException("Pay:Solana:Cluster must be mainnet-beta in Production");
        }

        var rpc = config["Pay:Solana:RpcUrl"]?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(rpc)
            || !Uri.TryCreate(rpc, UriKind.Absolute, out var rpcUri)
            || rpcUri.Scheme != Uri.UriSchemeHttps
            || rpcUri.IsLoopback
            || rpc.Contains("VITE_", StringComparison.Ordinal)
            || rpc.Contains("api.mainnet-beta.solana.com", StringComparison.OrdinalIgnoreCase)
            || rpc.Contains("api.devnet.solana.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pay:Solana:RpcUrl must be a public https RPC");
        }

        if (cluster == "mainnet-beta" && rpc.Contains("devnet", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pay:Solana:RpcUrl genesis hash mismatch");
        }

        if (cluster == "devnet" && rpc.Contains("mainnet", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pay:Solana:RpcUrl genesis hash mismatch");
        }
    }
}
