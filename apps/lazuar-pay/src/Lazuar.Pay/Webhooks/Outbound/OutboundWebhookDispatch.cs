using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Identity.OneWebhooks;
using Lazuar.Pay.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Webhooks.Outbound;

internal sealed class OutboundWebhookDispatch(PayDbContext db, SecretBox box, IHttpClientFactory http, IHostEnvironment env)
{
    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var pending = await db.OrgWebhookDeliveries
            .Where(x => x.Status == "pending" && x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var row in pending)
        {
            var endpoint = await db.OrgWebhookEndpoints.FindAsync([row.OrgId], ct);
            if (endpoint is null)
            {
                row.Status = "dead";
                row.LastError = "endpoint missing";
                continue;
            }

            // Re-resolve per attempt: the DNS answer at registration is not the answer at send
            // time. A URL that has come to resolve into private space dies here instead of
            // pointing signed payloads at the internal network. A failed lookup goes to send
            // and lands in the normal retry path. A malformed stored URL dies too — throwing
            // here would abort the batch and starve every delivery behind it.
            Uri destination;
            try
            {
                destination = new Uri(endpoint.Url, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                row.Status = "dead";
                row.LastError = "endpoint url invalid";
                continue;
            }

            var addresses = IPAddress.TryParse(destination.Host, out var literal)
                ? [literal]
                : await OutboundUrl.ResolveAsync(destination.Host, ct);
            if (!env.IsEnvironment("Testing") && !env.IsDevelopment()
                && addresses.Any(OutboundUrl.IsPrivateOrLoopback))
            {
                row.Status = "dead";
                row.LastError = "url resolves to a private address";
                continue;
            }

            var secret = box.Unprotect(endpoint.SecretCiphertext);
            var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var v1 = OneWebhookSignature.Compute(secret, row.PayloadJson, unix);
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
            {
                Content = new StringContent(row.PayloadJson, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", "v1=" + v1);
            req.Headers.TryAddWithoutValidation("X-Lazuar-Timestamp", unix.ToString());
            req.Headers.TryAddWithoutValidation("X-Lazuar-Event-Id", row.EventId);
            req.Headers.TryAddWithoutValidation("X-Lazuar-Event-Type", row.EventType);
            req.Headers.TryAddWithoutValidation("X-Lazuar-Tenant-Id", row.OrgId);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("Lazuar-Pay-Webhooks", "1.0"));

            HttpResponseMessage? response = null;
            try
            {
                var client = http.CreateClient("pay-webhooks");
                response = await client.SendAsync(req, ct);
                var code = (int)response.StatusCode;
                row.LastHttpStatus = code;
                row.AttemptCount += 1;
                if (code is >= 200 and < 300)
                {
                    row.Status = "succeeded";
                }
                else if (code is 401 or 403 or 410)
                {
                    row.Status = "dead";
                }
                else
                {
                    row.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 15 * row.AttemptCount));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                row.AttemptCount += 1;
                row.LastError = ex.GetType().Name;
                row.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 15 * row.AttemptCount));
            }
            finally
            {
                response?.Dispose();
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
