using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Modules.Lhdn.Infrastructure.Observability;

/// <summary>
/// Contributes Lhdn stuck TaxDocuments count into the platform metrics bag
/// (<see cref="PlatformMetricsCollector.LhdnStuckCountKey"/>).
/// </summary>
public sealed class LhdnStuckMetricsContributor : IPlatformMetricsContributor
{
    private readonly LhdnObservabilityOptions _options;
    private readonly ILogger<LhdnStuckMetricsContributor> _logger;

    public LhdnStuckMetricsContributor(
        IOptions<LhdnObservabilityOptions> options,
        ILogger<LhdnStuckMetricsContributor> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "lhdn";

    public async Task ContributeAsync(
        PlatformMetricsCollectContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var threshold = _options.StuckThreshold;
        if (threshold <= TimeSpan.Zero)
        {
            threshold = TimeSpan.FromHours(1);
        }

        var count = await QueryLhdnStuckAsync(context.Connection, threshold, cancellationToken);
        context.Bag.SetLong(PlatformMetricsCollector.LhdnStuckCountKey, count);
    }

    private async Task<long> QueryLhdnStuckAsync(
        System.Data.Common.DbConnection connection,
        TimeSpan olderThan,
        CancellationToken ct)
    {
        // PENDING/SUBMITTED and UpdatedAt older than threshold.
        const string sql = """
            SELECT COUNT(*)
            FROM lhdn."TaxDocuments"
            WHERE "ValidationStatus" IN ('PENDING', 'SUBMITTED')
              AND "UpdatedAt" < (NOW() AT TIME ZONE 'UTC') - @threshold
            """;

        try
        {
            if (connection is not NpgsqlConnection npgsql)
            {
                _logger.LogWarning(
                    "Lhdn stuck metrics expected NpgsqlConnection; got {ConnectionType}. Reporting 0.",
                    connection.GetType().FullName);
                return 0;
            }

            await using var cmd = new NpgsqlCommand(sql, npgsql);
            cmd.Parameters.AddWithValue("threshold", olderThan);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result is long l ? l : Convert.ToInt64(result ?? 0L);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable
            or PostgresErrorCodes.InvalidSchemaName)
        {
            return 0;
        }
    }
}
