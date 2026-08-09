using System;
using System.Text.RegularExpressions;
using BuildingBlocks.Application.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Observability;

/// <summary>DI helpers for platform outbox schema metrics registration.</summary>
public static class OutboxSchemaMetricsServiceCollectionExtensions
{
    private static readonly Regex SchemaNameRegex = new(
        @"^[a-z][a-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Registers <paramref name="schema"/> for outbox/inbox lag and dead-letter scraping.
    /// Call from each module's <c>Add*Module</c> next to outbox/inbox hosted services.
    /// </summary>
    /// <exception cref="ArgumentException">When schema is not a safe PostgreSQL identifier.</exception>
    public static IServiceCollection AddOutboxSchemaMetrics(
        this IServiceCollection services,
        string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        if (!SchemaNameRegex.IsMatch(schema))
        {
            throw new ArgumentException(
                $"Invalid outbox schema identifier '{schema}'. Must match ^[a-z][a-z0-9_]*$.",
                nameof(schema));
        }

        services.AddSingleton<IOutboxSchemaRegistration>(new OutboxSchemaRegistration(schema));
        return services;
    }
}
