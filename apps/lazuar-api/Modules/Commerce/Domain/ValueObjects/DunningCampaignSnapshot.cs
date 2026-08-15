using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Domain.ValueObjects;

public sealed record DunningCampaignSnapshotStep(
    Guid Id,
    int DayOffset,
    string ActionType,
    string? Subject,
    string? EmailBody,
    string? WhatsAppBody) : IDunningStepCopy;

/// <summary>
/// Immutable campaign definition captured at PAST_DUE assign. Unknown <c>v</c> is treated as corrupt.
/// </summary>
public sealed class DunningCampaignSnapshot
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public int Version { get; }
    public Guid CampaignId { get; }
    public DateTime CapturedAt { get; }
    public string Name { get; }
    public string FinalAction { get; }
    public int GracePeriodDays { get; }
    public IReadOnlyList<DunningCampaignSnapshotStep> Steps { get; }

    public DunningCampaignSnapshot(
        Guid campaignId,
        DateTime capturedAt,
        string name,
        string finalAction,
        int gracePeriodDays,
        IReadOnlyList<DunningCampaignSnapshotStep> steps,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(steps);

        Version = version;
        CampaignId = campaignId;
        CapturedAt = capturedAt.Kind switch
        {
            DateTimeKind.Utc => capturedAt,
            DateTimeKind.Local => capturedAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(capturedAt, DateTimeKind.Utc)
        };
        Name = name ?? string.Empty;
        FinalAction = string.IsNullOrWhiteSpace(finalAction) ? "NONE" : finalAction.ToUpperInvariant();
        GracePeriodDays = gracePeriodDays;
        Steps = steps.ToArray();
    }

    public static DunningCampaignSnapshot From(DunningCampaign campaign, DateTime? capturedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var steps = campaign.Steps
            .OrderBy(s => s.DayOffset)
            .Select(s => new DunningCampaignSnapshotStep(
                s.Id,
                s.DayOffset,
                s.ActionType,
                s.Subject,
                s.EmailBody,
                s.WhatsAppBody))
            .ToArray();

        return new DunningCampaignSnapshot(
            campaign.Id,
            capturedAtUtc ?? DateTime.UtcNow,
            campaign.Name,
            campaign.FinalAction,
            campaign.GracePeriodDays,
            steps);
    }

    /// <summary>Minimal v:1 object for tests that only need a campaign pin.</summary>
    public static DunningCampaignSnapshot Empty(Guid campaignId) =>
        new(campaignId, DateTime.UtcNow, string.Empty, "NONE", 0, Array.Empty<DunningCampaignSnapshotStep>());

    public string Serialize()
    {
        var document = new Document
        {
            V = Version,
            CampaignId = CampaignId,
            CapturedAt = CapturedAt,
            Name = Name,
            FinalAction = FinalAction,
            GracePeriodDays = GracePeriodDays,
            Steps = Steps.Select(s => new StepDocument
            {
                Id = s.Id,
                DayOffset = s.DayOffset,
                ActionType = s.ActionType,
                Subject = s.Subject,
                EmailBody = s.EmailBody,
                WhatsAppBody = s.WhatsAppBody
            }).ToList()
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    /// <summary>Returns null for empty, garbage, or unknown <c>v</c>. Does not throw.</summary>
    public static DunningCampaignSnapshot? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(json, JsonOptions);
            if (document == null
                || document.V != CurrentVersion
                || document.CampaignId == Guid.Empty)
            {
                return null;
            }

            var steps = (document.Steps ?? new List<StepDocument>())
                .Select(s => new DunningCampaignSnapshotStep(
                    s.Id,
                    s.DayOffset,
                    s.ActionType ?? string.Empty,
                    s.Subject,
                    s.EmailBody,
                    s.WhatsAppBody))
                .ToArray();

            return new DunningCampaignSnapshot(
                document.CampaignId,
                document.CapturedAt,
                document.Name ?? string.Empty,
                document.FinalAction ?? "NONE",
                document.GracePeriodDays,
                steps,
                document.V);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class Document
    {
        [JsonPropertyName("v")]
        public int V { get; set; }

        public Guid CampaignId { get; set; }
        public DateTime CapturedAt { get; set; }
        public string? Name { get; set; }
        public string? FinalAction { get; set; }
        public int GracePeriodDays { get; set; }
        public List<StepDocument>? Steps { get; set; }
    }

    private sealed class StepDocument
    {
        public Guid Id { get; set; }
        public int DayOffset { get; set; }
        public string? ActionType { get; set; }
        public string? Subject { get; set; }
        public string? EmailBody { get; set; }
        public string? WhatsAppBody { get; set; }
    }
}
