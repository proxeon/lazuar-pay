namespace BuildingBlocks.Application.Observability;

/// <summary>
/// Declares that a PostgreSQL schema participates in platform outbox/inbox metrics.
/// Modules register via <c>AddOutboxSchemaMetrics</c>; the BB aggregator scrapes only registered schemas.
/// </summary>
public interface IOutboxSchemaRegistration
{
    /// <summary>PostgreSQL schema that owns OutboxMessages / InboxMessages (allow-listed identifier).</summary>
    string Schema { get; }
}

/// <summary>Immutable schema registration for outbox metrics DI.</summary>
public sealed record OutboxSchemaRegistration(string Schema) : IOutboxSchemaRegistration;
