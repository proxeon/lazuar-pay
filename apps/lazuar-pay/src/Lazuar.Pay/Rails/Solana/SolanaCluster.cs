namespace Lazuar.Pay.Rails.Solana;

public static class SolanaCluster
{
    public const string Mainnet = "mainnet-beta";
    public const string Devnet = "devnet";
    public const string MainnetGenesis = "5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d";
    public const string DevnetGenesis = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";

    public static bool TryNormalize(string? raw, out string cluster)
    {
        cluster = (raw ?? "").Trim().ToLowerInvariant();
        if (cluster is "mainnet")
        {
            cluster = Mainnet;
        }

        return cluster is Mainnet or Devnet;
    }

    public static string FromConfig(IConfiguration config)
    {
        return TryNormalize(config["Pay:Solana:Cluster"], out var cluster) ? cluster : Devnet;
    }

    public static string VaultEnvironment(string cluster) =>
        cluster == Mainnet ? "mainnet" : "devnet";

    public static bool MatchesVault(string cluster, string? vaultEnvironment)
    {
        if (!PayProviders.TryNormalizeSolanaEnvironment(vaultEnvironment, out var env))
        {
            return false;
        }

        return env == VaultEnvironment(cluster);
    }

    public static string Mint(string cluster) =>
        cluster == Mainnet ? SolanaUsdc.MainnetMint : SolanaUsdc.DevnetMint;

    public static string GenesisHash(string cluster) =>
        cluster == Mainnet ? MainnetGenesis : DevnetGenesis;

    public static string? RpcUrl(IConfiguration config)
    {
        var url = config["Pay:Solana:RpcUrl"]?.Trim();
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    public static string? ParseGenesisHash(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var result) || result.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return null;
        }

        var hash = result.GetString();
        return string.IsNullOrWhiteSpace(hash) ? null : hash;
    }
}
