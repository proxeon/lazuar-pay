using System.Net.Http.Json;
using System.Text.Json;

namespace Lazuar.Pay.Rails.Solana;

public sealed class SolanaRpc(IHttpClientFactory http, IConfiguration config)
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<JsonDocument> GetTransactionAsync(string signature, CancellationToken ct)
    {
        var url = config["Pay:Solana:RpcUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Pay:Solana:RpcUrl is not configured");
        }

        var payload = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "getTransaction",
            @params = new object[]
            {
                signature,
                new Dictionary<string, object?>
                {
                    ["encoding"] = "jsonParsed",
                    ["commitment"] = "finalized",
                    ["maxSupportedTransactionVersion"] = 0
                }
            }
        };
        var client = http.CreateClient("solana");
        using var response = await client.PostAsJsonAsync(url, payload, Json, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("solana RPC rejected getTransaction");
        }

        return JsonDocument.Parse(body);
    }

    public async Task<JsonDocument> GetSignaturesForAddressAsync(string reference, CancellationToken ct)
    {
        var url = config["Pay:Solana:RpcUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Pay:Solana:RpcUrl is not configured");
        }

        var payload = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "getSignaturesForAddress",
            @params = new object[]
            {
                reference,
                new Dictionary<string, object?> { ["commitment"] = "finalized", ["limit"] = 5 }
            }
        };
        var client = http.CreateClient("solana");
        using var response = await client.PostAsJsonAsync(url, payload, Json, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("solana RPC rejected getSignaturesForAddress");
        }

        return JsonDocument.Parse(body);
    }
}
