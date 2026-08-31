using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Lazuar.Pay.Rails.Solana;

public sealed class SolanaRpcThrottledException() : InvalidOperationException("solana RPC throttled");

public sealed class SolanaRpc(IHttpClientFactory http, IConfiguration config)
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<JsonDocument> GetTransactionAsync(string signature, CancellationToken ct) =>
        await PostAsync(
            "getTransaction",
            new object[]
            {
                signature,
                new Dictionary<string, object?>
                {
                    ["encoding"] = "jsonParsed",
                    ["commitment"] = "finalized",
                    ["maxSupportedTransactionVersion"] = 0
                }
            },
            ct);

    public async Task<JsonDocument> GetSignaturesForAddressAsync(string reference, CancellationToken ct) =>
        await PostAsync(
            "getSignaturesForAddress",
            new object[]
            {
                reference,
                new Dictionary<string, object?> { ["commitment"] = "finalized", ["limit"] = 20 }
            },
            ct);

    public async Task<string> GetGenesisHashAsync(CancellationToken ct)
    {
        using var doc = await PostAsync("getGenesisHash", Array.Empty<object>(), ct);
        if (!doc.RootElement.TryGetProperty("result", out var result)
            || result.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(result.GetString()))
        {
            throw new InvalidOperationException("solana RPC returned no genesis hash");
        }

        return result.GetString()!;
    }

    async Task<JsonDocument> PostAsync(string method, object[] args, CancellationToken ct)
    {
        var url = SolanaCluster.RpcUrl(config);
        if (url is null)
        {
            throw new InvalidOperationException("Pay:Solana:RpcUrl is not configured");
        }

        var payload = new { jsonrpc = "2.0", id = 1, method, @params = args };
        var client = http.CreateClient("solana");
        using var response = await client.PostAsJsonAsync(url, payload, Json, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new SolanaRpcThrottledException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("solana RPC rejected " + method);
        }

        return JsonDocument.Parse(body);
    }
}
