using System.Text.Json;
using Lazuar.Pay.Identity.Client;

namespace Lazuar.Pay.Webhooks.Outbound;

internal static class PayWebhookEnvelope
{
    public const string Completed = "payment.completed";
    public const string Test = "webhook.test";

    public static string Serialize(string type, string id, string orgId, object data) =>
        JsonSerializer.Serialize(new
        {
            id,
            type,
            created_at = DateTimeOffset.UtcNow,
            org_id = orgId,
            api_version = "0.1.0",
            data
        }, OneClient.Json);
}
