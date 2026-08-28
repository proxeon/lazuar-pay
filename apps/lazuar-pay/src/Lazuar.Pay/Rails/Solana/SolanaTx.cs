using System.Text.Json;
using Lazuar.Pay.Data;

namespace Lazuar.Pay.Rails.Solana;

public static class SolanaTx
{
    public const string MemoProgram = "MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr";

    public static string? Validate(
        JsonDocument rpc,
        CheckoutRow checkout,
        GatewayCredentialRow cred,
        string signature)
    {
        if (!rpc.RootElement.TryGetProperty("result", out var result) || result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "transaction not found";
        }

        if (result.TryGetProperty("meta", out var meta)
            && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("err", out var err)
            && err.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return "transaction failed";
        }

        if (!result.TryGetProperty("transaction", out var tx))
        {
            return "transaction missing";
        }

        var message = tx.TryGetProperty("message", out var msg) ? msg : default;
        if (message.ValueKind != JsonValueKind.Object)
        {
            return "transaction missing";
        }

        var mint = SolanaUsdc.MintFor(cred.Environment);
        if (!TryTransfer(message, out var foundMint, out var atomic, out var programId))
        {
            return "transfer missing";
        }

        if (programId == SolanaUsdc.Token2022Program)
        {
            return "token program mismatch";
        }

        if (!string.Equals(foundMint, mint, StringComparison.Ordinal))
        {
            return "mint mismatch";
        }

        if (!SolanaMoney.TryToAtomic(checkout.Amount, out var expected) || atomic != expected)
        {
            return "amount mismatch";
        }

        if (!HasReference(message, checkout.ProviderSessionId))
        {
            return "reference missing";
        }

        if (!HasMemo(message, checkout.Id))
        {
            return "memo mismatch";
        }

        if (!OwnerMatches(result, cred.PublicMerchantId, mint))
        {
            return "destination mismatch";
        }

        if (tx.TryGetProperty("signatures", out var sigs) && sigs.ValueKind == JsonValueKind.Array)
        {
            var listed = sigs.EnumerateArray().Select(x => x.GetString()).ToList();
            if (listed.Count > 0 && !listed.Contains(signature, StringComparer.Ordinal))
            {
                return "signature mismatch";
            }
        }

        return null;
    }

    static bool TryTransfer(JsonElement message, out string mint, out long atomic, out string programId)
    {
        mint = "";
        atomic = 0;
        programId = "";
        if (!message.TryGetProperty("instructions", out var ixs) || ixs.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var ix in ixs.EnumerateArray())
        {
            programId = ix.TryGetProperty("programId", out var pid) ? pid.GetString() ?? "" : "";
            if (!ix.TryGetProperty("parsed", out var parsed) || parsed.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = parsed.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type != "transferChecked")
            {
                continue;
            }

            if (!parsed.TryGetProperty("info", out var info))
            {
                continue;
            }

            mint = info.TryGetProperty("mint", out var m) ? m.GetString() ?? "" : "";
            if (!info.TryGetProperty("tokenAmount", out var ta)
                || !ta.TryGetProperty("amount", out var amt)
                || !long.TryParse(amt.GetString(), out atomic))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(mint);
        }

        return false;
    }

    static bool HasReference(JsonElement message, string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)
            || !message.TryGetProperty("accountKeys", out var keys)
            || keys.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var key in keys.EnumerateArray())
        {
            var pk = key.ValueKind == JsonValueKind.String
                ? key.GetString()
                : key.TryGetProperty("pubkey", out var p) ? p.GetString() : null;
            if (string.Equals(pk, reference, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasMemo(JsonElement message, string checkoutId)
    {
        if (!message.TryGetProperty("instructions", out var ixs) || ixs.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var ix in ixs.EnumerateArray())
        {
            var programId = ix.TryGetProperty("programId", out var pid) ? pid.GetString() : null;
            if (programId != MemoProgram)
            {
                continue;
            }

            if (!ix.TryGetProperty("parsed", out var parsed))
            {
                continue;
            }

            var text = parsed.ValueKind == JsonValueKind.String
                ? parsed.GetString()
                : parsed.TryGetProperty("info", out var info) && info.TryGetProperty("memo", out var memo)
                    ? memo.GetString()
                    : parsed.TryGetProperty("memo", out var m)
                        ? m.GetString()
                        : null;
            if (string.Equals(text, checkoutId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    static bool OwnerMatches(JsonElement result, string? merchant, string mint)
    {
        if (string.IsNullOrWhiteSpace(merchant)
            || !result.TryGetProperty("meta", out var meta)
            || !meta.TryGetProperty("postTokenBalances", out var bals)
            || bals.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var b in bals.EnumerateArray())
        {
            var owner = b.TryGetProperty("owner", out var o) ? o.GetString() : null;
            var bMint = b.TryGetProperty("mint", out var m) ? m.GetString() : null;
            if (string.Equals(owner, merchant, StringComparison.Ordinal)
                && string.Equals(bMint, mint, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
