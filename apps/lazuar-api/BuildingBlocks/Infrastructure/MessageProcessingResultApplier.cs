namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Shared success/failure state transitions for outbox and inbox rows
/// (attempt count, backoff, dead-letter).
/// </summary>
public static class MessageProcessingResultApplier
{
    public static void ApplySuccess(IMessageProcessingState msg, DateTime utcNow)
    {
        msg.ProcessedAt = utcNow;
        msg.Error = null;
        msg.NextAttemptAt = null;
    }

    public static void ApplyFailure(IMessageProcessingState msg, Exception ex, DateTime utcNow)
    {
        msg.AttemptCount++;
        msg.Error = ex.ToString();

        if (msg.AttemptCount >= MessageRetryPolicy.MaxAttempts)
        {
            msg.Status = MessageProcessingStatus.Dead;
            msg.ProcessedAt = utcNow;
            msg.NextAttemptAt = null;
        }
        else
        {
            msg.NextAttemptAt = utcNow + MessageRetryPolicy.GetBackoff(msg.AttemptCount);
        }
    }
}

/// <summary>
/// Common retry/dead-letter fields on <see cref="OutboxMessage"/> and <see cref="InboxMessage"/>.
/// </summary>
public interface IMessageProcessingState
{
    int AttemptCount { get; set; }
    DateTime? NextAttemptAt { get; set; }
    string Status { get; set; }
    string? Error { get; set; }
    DateTime? ProcessedAt { get; set; }
}
