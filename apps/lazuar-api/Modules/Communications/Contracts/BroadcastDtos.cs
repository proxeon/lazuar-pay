using System;
using System.Text.Json.Serialization;

namespace Modules.Communications.Contracts;

/// <summary>Status of a broadcast fan-out.</summary>
public record BroadcastStatusDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("total_recipients")] public int TotalRecipients { get; init; }
    [JsonPropertyName("sent_count")] public int SentCount { get; init; }
    [JsonPropertyName("suppressed_count")] public int SuppressedCount { get; init; }
    [JsonPropertyName("failed_count")] public int FailedCount { get; init; }
    [JsonPropertyName("credits_reserved")] public int CreditsReserved { get; init; }
    [JsonPropertyName("credits_used")] public int CreditsUsed { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("completed_at")] public DateTimeOffset? CompletedAt { get; init; }
    [JsonPropertyName("failure_reason")] public string? FailureReason { get; init; }
}

/// <summary>Pre-send cost estimate for a broadcast.</summary>
public record BroadcastCostPreviewDto
{
    [JsonPropertyName("recipient_count")] public int RecipientCount { get; init; }
    [JsonPropertyName("credits_per_recipient")] public int CreditsPerRecipient { get; init; }
    [JsonPropertyName("total_credits")] public int TotalCredits { get; init; }
    [JsonPropertyName("sufficient_credits")] public bool SufficientCredits { get; init; }
    [JsonPropertyName("available_credits")] public int AvailableCredits { get; init; }
}
