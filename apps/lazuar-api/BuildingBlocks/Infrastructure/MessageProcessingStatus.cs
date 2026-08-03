namespace BuildingBlocks.Infrastructure;

public static class MessageProcessingStatus
{
    public const string Pending = "Pending";
    public const string Dead = "Dead";
}

public static class MessageRetryPolicy
{
    public const int MaxAttempts = 5;

    public static TimeSpan GetBackoff(int attemptCountAfterIncrement)
        => TimeSpan.FromMinutes(Math.Pow(2, attemptCountAfterIncrement));
}
