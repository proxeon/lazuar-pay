using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.PublicPay;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

using Lazuar.Pay.Rails;

namespace Lazuar.Pay.Rails.Chip;

public sealed class ChipHosted(PayDbContext db, SecretBox box, IHttpClientFactory http, IConfiguration config, IHostEnvironment env) : IHostedRail
{
    public const string ApiBase = "https://gate.chip-in.asia/api/v1/";

    public string Provider => PayProviders.Chip;

    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Chip, ct);
        if (cred is null || string.IsNullOrWhiteSpace(cred.PublicMerchantId))
        {
            throw new InvalidOperationException("rail not configured");
        }

        if (!BuyerEmail.IsUsable(checkout.PayerEmail))
        {
            throw new InvalidOperationException("email is required");
        }

        var payload = new Dictionary<string, object?>
        {
            ["brand_id"] = cred.PublicMerchantId,
            ["client"] = new
            {
                email = checkout.PayerEmail!.Trim(),
                full_name = BuyerEmail.NameFrom(checkout.PayerEmail, checkout.PayerName)
            },
            ["purchase"] = new
            {
                currency = checkout.Currency,
                products = new[]
                {
                    new { name = "Pay", price = MoneyMath.ToMinor(checkout.Amount) }
                },
                metadata = new Dictionary<string, string>
                {
                    ["checkout_id"] = checkout.Id,
                    ["org_id"] = checkout.OrgId
                }
            },
            ["success_redirect"] = CheckoutUrls.Success(checkout, config, env),
            ["failure_redirect"] = CheckoutUrls.Cancel(checkout, config, env),
            ["cancel_redirect"] = CheckoutUrls.Cancel(checkout, config, env)
        };

        var client = http.CreateClient("chip");
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "purchases/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", box.Unprotect(cred.Ciphertext));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "lazuar-checkout:" + checkout.Id);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("CHIP rejected the org key");
        }

        using var doc = JsonDocument.Parse(body);
        var url = doc.RootElement.TryGetProperty("checkout_url", out var u) ? u.GetString() : null;
        var id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("CHIP returned no URL");
        }

        return new HostedSession(url, id);
    }
}
