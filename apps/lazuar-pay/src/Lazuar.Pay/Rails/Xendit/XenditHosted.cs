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

namespace Lazuar.Pay.Rails.Xendit;

public sealed class XenditHosted(PayDbContext db, SecretBox box, IHttpClientFactory http, IConfiguration config, IHostEnvironment env) : IHostedRail
{
    public const string ApiBase = "https://api.xendit.co";

    public string Provider => PayProviders.Xendit;

    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Xendit, ct);
        if (cred is null)
        {
            throw new InvalidOperationException("rail not configured");
        }

        if (!BuyerEmail.IsUsable(checkout.PayerEmail))
        {
            throw new InvalidOperationException("email is required");
        }

        if (!MoneyMath.TryNormalizeCurrency(checkout.Currency, out var currency))
        {
            throw new InvalidOperationException("Currency is required.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["external_id"] = checkout.Id,
            ["amount"] = MoneyMath.FromMinor(MoneyMath.ToMinor(checkout.Amount)),
            ["currency"] = currency,
            ["description"] = "Pay",
            ["payer_email"] = checkout.PayerEmail!.Trim(),
            ["success_redirect_url"] = CheckoutUrls.Success(checkout, config, env),
            ["failure_redirect_url"] = CheckoutUrls.Cancel(checkout, config, env),
            ["metadata"] = new Dictionary<string, string>
            {
                ["checkout_id"] = checkout.Id,
                ["org_id"] = checkout.OrgId
            }
        };

        var client = http.CreateClient("xendit");
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "/v2/invoices");
        var secret = box.Unprotect(cred.Ciphertext);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(secret + ":")));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "lazuar-checkout:" + checkout.Id);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Xendit rejected the org key");
        }

        using var doc = JsonDocument.Parse(body);
        var url = doc.RootElement.TryGetProperty("invoice_url", out var u) ? u.GetString() : null;
        var id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Xendit returned no URL");
        }

        return new HostedSession(url, id);
    }
}
