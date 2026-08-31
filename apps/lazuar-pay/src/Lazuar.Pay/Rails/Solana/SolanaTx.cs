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
        string signature,
        string cluster)
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

        var mint = SolanaCluster.Mint(cluster);
        if (!SolanaMoney.TryToAtomic(checkout.Amount, out var expected))
        {
            return "amount mismatch";
        }

        var transfer = TransferMismatch(message, result, cred.PublicMerchantId, mint, expected);
        if (transfer is not null)
        {
            return transfer;
        }

        if (!HasReference(message, checkout.ProviderSessionId))
        {
            return "reference missing";
        }

        if (!HasMemo(message, checkout.Id))
        {
            return "memo mismatch";
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

    static string? TransferMismatch(
        JsonElement message,
        JsonElement result,
        string? merchant,
        string mint,
        long expected)
    {
        if (string.IsNullOrWhiteSpace(merchant))
        {
            return "destination mismatch";
        }

        if (!message.TryGetProperty("instructions", out var ixs) || ixs.ValueKind != JsonValueKind.Array)
        {
            return "transfer missing";
        }

        if (!result.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object)
        {
            return "destination mismatch";
        }

        var keys = AccountPubkeys(message);
        var pre = TokenBalances(meta, "preTokenBalances");
        var post = TokenBalances(meta, "postTokenBalances");
        var anyTransfer = false;
        var token2022ToMerchant = false;
        var boundWrongMint = false;
        var boundWrongAmount = false;

        foreach (var ix in ixs.EnumerateArray())
        {
            var programId = ix.TryGetProperty("programId", out var pid) ? pid.GetString() ?? "" : "";
            if (!ix.TryGetProperty("parsed", out var parsed) || parsed.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = parsed.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type != "transferChecked")
            {
                continue;
            }

            anyTransfer = true;
            if (!parsed.TryGetProperty("info", out var info))
            {
                continue;
            }

            var dest = info.TryGetProperty("destination", out var d) ? d.GetString() ?? "" : "";
            var foundMint = info.TryGetProperty("mint", out var m) ? m.GetString() ?? "" : "";
            if (!info.TryGetProperty("tokenAmount", out var ta) || !TryAtomic(ta, out var atomic))
            {
                continue;
            }

            var destIndex = keys.FindIndex(k => string.Equals(k, dest, StringComparison.Ordinal));
            if (destIndex < 0 || !post.TryGetValue(destIndex, out var postRow))
            {
                continue;
            }

            if (!string.Equals(postRow.Owner, merchant, StringComparison.Ordinal))
            {
                continue;
            }

            if (programId == SolanaUsdc.Token2022Program)
            {
                token2022ToMerchant = true;
                continue;
            }

            if (programId != SolanaUsdc.TokenProgram)
            {
                continue;
            }

            if (!string.Equals(foundMint, mint, StringComparison.Ordinal)
                || !string.Equals(postRow.Mint, mint, StringComparison.Ordinal))
            {
                boundWrongMint = true;
                continue;
            }

            var preAmt = pre.TryGetValue(destIndex, out var preRow) ? preRow.Amount : 0;
            if (atomic != expected || postRow.Amount - preAmt != expected)
            {
                boundWrongAmount = true;
                continue;
            }

            return null;
        }

        if (token2022ToMerchant)
        {
            return "token program mismatch";
        }

        if (boundWrongMint)
        {
            return "mint mismatch";
        }

        if (boundWrongAmount)
        {
            return "amount mismatch";
        }

        return anyTransfer ? "destination mismatch" : "transfer missing";
    }

    static List<string> AccountPubkeys(JsonElement message)
    {
        var keys = new List<string>();
        if (!message.TryGetProperty("accountKeys", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return keys;
        }

        foreach (var key in arr.EnumerateArray())
        {
            var pk = key.ValueKind == JsonValueKind.String
                ? key.GetString()
                : key.TryGetProperty("pubkey", out var p) ? p.GetString() : null;
            if (!string.IsNullOrWhiteSpace(pk))
            {
                keys.Add(pk);
            }
        }

        return keys;
    }

    static Dictionary<int, (string Owner, string Mint, long Amount)> TokenBalances(JsonElement meta, string name)
    {
        var map = new Dictionary<int, (string Owner, string Mint, long Amount)>();
        if (!meta.TryGetProperty(name, out var bals) || bals.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        foreach (var b in bals.EnumerateArray())
        {
            if (!b.TryGetProperty("accountIndex", out var idxEl) || !idxEl.TryGetInt32(out var idx))
            {
                continue;
            }

            var owner = b.TryGetProperty("owner", out var o) ? o.GetString() ?? "" : "";
            var bMint = b.TryGetProperty("mint", out var m) ? m.GetString() ?? "" : "";
            if (!b.TryGetProperty("uiTokenAmount", out var ui) || !TryAtomic(ui, out var amount))
            {
                continue;
            }

            map[idx] = (owner, bMint, amount);
        }

        return map;
    }

    static bool TryAtomic(JsonElement tokenAmount, out long atomic)
    {
        atomic = 0;
        if (!tokenAmount.TryGetProperty("amount", out var amt))
        {
            return false;
        }

        if (amt.ValueKind == JsonValueKind.String)
        {
            return long.TryParse(amt.GetString(), out atomic);
        }

        return amt.ValueKind == JsonValueKind.Number && amt.TryGetInt64(out atomic);
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
}
