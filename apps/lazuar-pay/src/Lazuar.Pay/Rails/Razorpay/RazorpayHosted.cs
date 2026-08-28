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

using Lazuar.Pay.Rails;

namespace Lazuar.Pay.Rails.Razorpay;

public sealed class RazorpayHosted(PayDbContext db, SecretBox box, IHttpClientFactory http, IConfiguration config, IHostEnvironment env) : IHostedRail
{
    public const string ApiBase = "https://api.razorpay.com/v1/";

    public string Provider => PayProviders.Razorpay;

    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Razorpay, ct);
        if (cred is null)
        {
            throw new InvalidOperationException("rail not configured");
        }

        if (!BuyerEmail.IsUsable(checkout.PayerEmail))
        {
            throw new InvalidOperationException("email is required");
        }

        if (!TrySplit(box.Unprotect(cred.Ciphertext), out var keyId, out var keySecret))
        {
            throw new InvalidOperationException("rail not configured");
        }

        if (!MoneyMath.TryNormalizeCurrency(checkout.Currency, out var currency))
        {
            throw new InvalidOperationException("Currency is required.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["amount"] = MoneyMath.ToMinor(checkout.Amount),
            ["currency"] = currency,
            ["description"] = "Pay",
            ["customer"] = new { email = checkout.PayerEmail!.Trim(), name = BuyerEmail.NameFrom(checkout.PayerEmail, checkout.PayerName) },
            ["notes"] = new Dictionary<string, string>
            {
                ["checkout_id"] = checkout.Id,
                ["org_id"] = checkout.OrgId
            },
            ["callback_url"] = CheckoutUrls.Success(checkout, config, env),
            ["callback_method"] = "get"
        };

        var client = http.CreateClient("razorpay");
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "payment_links");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(keyId + ":" + keySecret)));
        request.Headers.TryAddWithoutValidation("X-Razorpay-Idempotency", "lazuar-checkout:" + checkout.Id);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Razorpay rejected the org key");
        }

        using var doc = JsonDocument.Parse(body);
        var url = doc.RootElement.TryGetProperty("short_url", out var u) ? u.GetString() : null;
        var id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Razorpay returned no URL");
        }

        return new HostedSession(url, id);
    }

    internal static bool TrySplit(string secret, out string keyId, out string keySecret)
    {
        keyId = "";
        keySecret = "";
        var i = secret.IndexOf(':');
        if (i <= 0 || i == secret.Length - 1)
        {
            return false;
        }

        keyId = secret[..i];
        keySecret = secret[(i + 1)..];
        return true;
    }
}
