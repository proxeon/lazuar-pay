// apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnSubmissionJob.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Contracts.Events;
using Modules.Lhdn.Domain;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Workers;

public class LhdnSubmissionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LhdnSubmissionJob> _logger;
    private readonly BackgroundWorkerOptions _options;

    public LhdnSubmissionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<LhdnSubmissionJob> logger,
        IOptions<BackgroundWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDocumentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in LhdnSubmissionJob.");
            }

            await Task.Delay(_options.LhdnSubmissionInterval, stoppingToken);
        }
    }

    /// <summary>One poll cycle (hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => ProcessPendingDocumentsAsync(ct);

    private async Task ProcessPendingDocumentsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LhdnDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<ILhdnGatewayAdapter>();
        var secretVault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("LhdnEventBus");

        var leaseUntil = DateTime.UtcNow.Add(_options.ClaimLeaseDuration);
        var pendingDocs = await ClaimPendingDocumentsAsync(db, leaseUntil, ct);
        if (pendingDocs.Count == 0) return;

        foreach (var doc in pendingDocs)
        {
            try
            {
                var config = await db.TenantConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.OrganizationId == doc.OrganizationId, ct);
                if (config == null || string.IsNullOrWhiteSpace(config.MyInvoisClientId) || string.IsNullOrWhiteSpace(config.MyInvoisClientSecret))
                {
                    doc.MarkAsFailed("Tenant configuration or API credentials missing.");
                    continue;
                }

                var base64Document = Convert.ToBase64String(Encoding.UTF8.GetBytes(doc.RawXmlContent));

                var format = MyInvoisBuyerRules.DetectSubmissionFormat(doc.RawXmlContent);
                var payload = new
                {
                    documents = new[]
                    {
                        new
                        {
                            format,
                            documentHash = doc.DocumentHash,
                            codeNumber = doc.InternalReferenceId,
                            document = base64Document
                        }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);

                var clientSecret = secretVault.DecryptOrPlaintext(config.MyInvoisClientSecret);
                var token = await gateway.GetTokenAsync(config.OrganizationId, config.MyInvoisClientId, clientSecret, config.IntermediaryMode, config.SupplierTin, ct);
                var result = await gateway.SubmitDocumentAsync(config.MyInvoisClientId, token, jsonPayload, config.IntermediaryMode, config.SupplierTin, ct);

                if (result.Success && !string.IsNullOrEmpty(result.SubmissionUid))
                {
                    doc.MarkAsSubmitted(result.SubmissionUid, result.Uuid);

                    await eventBus.PublishAsync(new LhdnDocumentSubmittedIntegrationEvent(doc.OrganizationId, doc.InternalReferenceId, doc.IsTestMode));
                }
                else
                {
                    if (result.RetryAfterSeconds.HasValue)
                    {
                        doc.DelayPendingSubmission(result.RetryAfterSeconds.Value);
                    }
                    else
                    {
                        doc.MarkAsFailed(result.ErrorMessage ?? "Unknown gateway error.");
                    }
                }
            }
            catch (Exception ex)
            {
                doc.MarkAsFailed(ex.Message);
            }
            finally
            {
                await db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// Claims PENDING docs due for attempt with FOR UPDATE SKIP LOCKED, leases via NextPollAt, commits before gateway I/O.
    /// </summary>
    internal static async Task<List<TaxDocument>> ClaimPendingDocumentsAsync(
        LhdnDbContext db,
        DateTime leaseUntilUtc,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        List<TaxDocument> pendingDocs;

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            const string sql = """
                SELECT * FROM lhdn."TaxDocuments"
                WHERE "ValidationStatus" = 'PENDING'
                  AND ("NextPollAt" IS NULL OR "NextPollAt" <= NOW())
                ORDER BY "CreatedAt"
                LIMIT 50
                FOR UPDATE SKIP LOCKED;
                """;

            pendingDocs = await db.TaxDocuments
                .FromSqlRaw(sql)
                .IgnoreQueryFilters()
                .ToListAsync(ct);

            if (pendingDocs.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return pendingDocs;
            }

            foreach (var doc in pendingDocs)
            {
                doc.ClaimProcessingLease(leaseUntilUtc);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else
        {
            // InMemory / non-relational tests: no SKIP LOCKED.
            pendingDocs = await db.TaxDocuments
                .IgnoreQueryFilters()
                .Where(d => d.ValidationStatus == "PENDING" && (d.NextPollAt == null || d.NextPollAt <= now))
                .OrderBy(d => d.CreatedAt)
                .Take(50)
                .ToListAsync(ct);

            if (pendingDocs.Count == 0) return pendingDocs;

            foreach (var doc in pendingDocs)
            {
                doc.ClaimProcessingLease(leaseUntilUtc);
            }

            await db.SaveChangesAsync(ct);
        }

        return pendingDocs;
    }
}
