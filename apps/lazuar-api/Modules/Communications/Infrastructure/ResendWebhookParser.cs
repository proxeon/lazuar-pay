using System;
using System.Text.Json;

namespace Modules.Communications.Infrastructure;

/// <summary>
/// Accepts both Resend send-shape tags (array of {name,value}) and webhook-shape tags (object map),
/// plus recipient paths <c>data.to[0]</c>, <c>data.email.to[0]</c>, <c>data.recipient</c>.
/// </summary>
public static class ResendWebhookParser
{
    public static bool TryParseSuppression(
        string rawBody,
        out string? eventType,
        out string? recipient,
        out Guid? organizationId)
    {
        eventType = null;
        recipient = null;
        organizationId = null;

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            eventType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            var data = root.TryGetProperty("data", out var d) ? d : root;
            recipient = ReadRecipient(data);
            organizationId = ReadOrgTag(data);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? MapReason(string? eventType) => eventType switch
    {
        "email.bounced" => "BOUNCE",
        "email.complained" => "COMPLAINT",
        _ => null
    };

    private static string? ReadRecipient(JsonElement data)
    {
        if (data.TryGetProperty("to", out var toEl) && toEl.ValueKind == JsonValueKind.Array && toEl.GetArrayLength() > 0)
        {
            return toEl[0].GetString();
        }

        if (data.TryGetProperty("email", out var emailEl)
            && emailEl.ValueKind == JsonValueKind.Object
            && emailEl.TryGetProperty("to", out var nestedTo)
            && nestedTo.ValueKind == JsonValueKind.Array
            && nestedTo.GetArrayLength() > 0)
        {
            return nestedTo[0].GetString();
        }

        if (data.TryGetProperty("recipient", out var recipEl))
        {
            return recipEl.GetString();
        }

        return null;
    }

    private static Guid? ReadOrgTag(JsonElement data)
    {
        if (!data.TryGetProperty("tags", out var tagsEl))
        {
            return null;
        }

        if (tagsEl.ValueKind == JsonValueKind.Object
            && tagsEl.TryGetProperty("org", out var orgEl)
            && Guid.TryParse(orgEl.GetString(), out var fromObject))
        {
            return fromObject;
        }

        if (tagsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsEl.EnumerateArray())
            {
                var name = tag.TryGetProperty("name", out var n) ? n.GetString() : null;
                var value = tag.TryGetProperty("value", out var v) ? v.GetString() : null;
                if (name == "org" && Guid.TryParse(value, out var fromArray))
                {
                    return fromArray;
                }
            }
        }

        return null;
    }
}
