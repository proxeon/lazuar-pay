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
        var npgsql = db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;

        List<OrgWebhookDeliveryRow> pending;
        if (npgsql)
        {
            // Issue 005 (issues/001): claim the batch with a lease stamp under
            // FOR UPDATE SKIP LOCKED. Two replicas used to read the same pending rows and
            // double-send every delivery; and because the whole batch was persisted with one
            // SaveChanges at the end, any mid-loop throw (an undecryptable endpoint secret
            // after key rotation, say) discarded every row's outcome — starving all
            // deliveries behind the poison row, or re-POSTing the rows before it forever.
            // The 60s lease outlives the 10s send timeout; a crashed worker's rows become
            // claimable again after it expires.
            var leaseUntil = now.AddSeconds(60);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE public.org_webhook_deliveries AS d
                SET "NextAttemptAt" = {leaseUntil}
                FROM (
                    SELECT "Id" FROM public.org_webhook_deliveries
                    WHERE "Status" = 'pending' AND "NextAttemptAt" <= {now}
                    ORDER BY "CreatedAt"
                    LIMIT 20
                    FOR UPDATE SKIP LOCKED
                ) AS pick
                WHERE d."Id" = pick."Id"
                """, ct);
            pending = await db.OrgWebhookDeliveries
                .Where(x => x.Status == "pending" && x.NextAttemptAt == leaseUntil)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);
        }
        else
        {
            pending = await db.OrgWebhookDeliveries
                .Where(x => x.Status == "pending" && x.NextAttemptAt <= now)
                .OrderBy(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(ct);
        }

        foreach (var row in pending)
        {
            try
            {
                await DeliverAsync(row, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Issue 005: a poison row (undecryptable secret, unresolvable host, anything)
                // errors ITSELF and the batch moves on — it can no longer abort the pipeline.
                row.AttemptCount += 1;
                row.LastError = "dispatch:" + ex.GetType().Name;
                row.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 15 * row.AttemptCount));
            }

            // Per-row persistence: whatever happened to this row survives even if the next
            // row throws — the old single end-of-loop SaveChanges is what let one bad row
            // erase the outcomes of every row before it.
            await db.SaveChangesAsync(ct);
        }
    }

    async Task DeliverAsync(OrgWebhookDeliveryRow row, CancellationToken ct)
    {
        var endpoint = await db.OrgWebhookEndpoints.FindAsync([row.OrgId], ct);
        if (endpoint is null)
        {
            row.Status = "dead";
            row.LastError = "endpoint missing";
            return;
        }

        // Re-resolve per attempt: the DNS answer at registration is not the answer at send
        // time. A URL that has come to resolve into private space dies here instead of
        // pointing signed payloads at the internal network. A failed lookup goes to send
        // and lands in the normal retry path. A malformed stored URL dies too — throwing
        // here used to abort the batch and starve every delivery behind it (issue 005).
        Uri destination;
        try
        {
            destination = new Uri(endpoint.Url, UriKind.Absolute);
        }
        catch (UriFormatException)
        {
            row.Status = "dead";
            row.LastError = "endpoint url invalid";
            return;
        }

        var addresses = IPAddress.TryParse(destination.Host, out var literal)
            ? [literal]
            : await OutboundUrl.ResolveAsync(destination.Host, ct);
        if (!env.IsEnvironment("Testing") && !env.IsDevelopment()
            && addresses.Any(OutboundUrl.IsPrivateOrLoopback))
        {
            row.Status = "dead";
            row.LastError = "url resolves to a private address";
            return;
        }

        // Issue 017: this pre-send check is advisory. The connection itself is pinned to a
        // connect-time-validated address by the pay-webhooks ConnectCallback (see Program.cs)
        // — HttpClient re-resolving the hostname after this check was the DNS-rebinding hole.
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
}
