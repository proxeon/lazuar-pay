namespace Lazuar.Pay.Rails.Solana;

public static class SolanaUsdc
{
    public const string Currency = "USDC";
    public const int Decimals = 6;
    public const string MainnetMint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
    public const string DevnetMint = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";
    public const string TokenProgram = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    public const string Token2022Program = "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb";

    public static string MintFor(string environment) =>
        environment == "mainnet" ? MainnetMint : DevnetMint;

    public static bool IsPinnedMint(string mint) =>
        mint is MainnetMint or DevnetMint;
}
