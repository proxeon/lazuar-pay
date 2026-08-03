namespace BuildingBlocks.Infrastructure;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Type { get; set; } = "";
    public string Data { get; set; } = "";
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int AttemptCount { get; set; } = 0;
    public DateTime? NextAttemptAt { get; set; }
    public string Status { get; set; } = MessageProcessingStatus.Pending;
}
