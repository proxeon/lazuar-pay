using Lazuar.Pay.Webhooks;

namespace Lazuar.Pay.Rails.Solana;

internal static class SolanaWebhook
{
    public static PspParseResult Parse(string json, IHeaderDictionary headers)
    {
        _ = json;
        _ = headers;
        throw new PspVerifyException("solana does not use inbound PSP webhooks");
    }
}
