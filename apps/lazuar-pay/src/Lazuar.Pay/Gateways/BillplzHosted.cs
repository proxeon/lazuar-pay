using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Gateways;

public sealed class BillplzHosted(PayDbContext db, SecretBox box, IHttpClientFactory http, IConfiguration config, IHostEnvironment env) : IHostedRail
{
    public string Provider => PayProviders.Billplz;

    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Billplz, ct);
        if (cred is null || string.IsNullOrWhiteSpace(cred.PublicMerchantId))
        {
            throw new InvalidOperationException("rail not configured");
        }

        if (!BuyerEmail.IsUsable(checkout.PayerEmail))
        {
            throw new InvalidOperationException("email is required");
        }

        if (!TryPublicBase(config["Pay:PublicBaseUrl"], out var publicBase, out var baseError))
        {
            throw new InvalidOperationException(baseError);
        }

        var host = string.Equals(cred.Environment, "live", StringComparison.OrdinalIgnoreCase)
            ? "https://www.billplz.com/api/v3/"
            : "https://www.billplz-sandbox.com/api/v3/";
        var callback = $"{publicBase}/v1/webhooks/billplz/{checkout.OrgId}?checkout_id={Uri.EscapeDataString(checkout.Id)}";
        var payload = new Dictionary<string, object?>
        {
            ["collection_id"] = cred.PublicMerchantId,
            ["email"] = checkout.PayerEmail!.Trim(),
            ["name"] = BuyerEmail.NameFrom(checkout.PayerEmail, checkout.PayerName),
            ["amount"] = MoneyMath.ToMinor(checkout.Amount),
            ["description"] = "Pay",
            ["callback_url"] = callback,
            ["redirect_url"] = CheckoutUrls.Success(checkout, config, env),
            ["reference_1_label"] = "Checkout",
            ["reference_1"] = checkout.Id
        };

        var client = http.CreateClient("billplz");
        using var request = new HttpRequestMessage(HttpMethod.Post, host + "bills");
        var apiKey = box.Unprotect(cred.Ciphertext);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey + ":")));
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Billplz rejected the org key");
        }

        using var doc = JsonDocument.Parse(body);
        var url = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
        var id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Billplz returned no URL");
        }

        return new HostedSession(url, id);
    }

    internal static bool TryPublicBase(string? raw, out string callbackBase, out string error)
    {
        callbackBase = "";
        error = "";
        var value = (raw ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "callback base not public";
            return false;
        }

        var host = uri.Host;
        var loopback = uri.IsLoopback
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || host.Contains("lazuar-local-dev.com", StringComparison.OrdinalIgnoreCase);
        if (loopback)
        {
            error = "callback base not public";
            return false;
        }

        callbackBase = value;
        return true;
    }
}
