// apps/lazuar-api/Modules/Lhdn/Infrastructure/Workers/LhdnStatusPollingJob.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Contracts.Events;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Workers;

public class LhdnStatusPollingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LhdnStatusPollingJob> _logger;
    private readonly BackgroundWorkerOptions _options;

    public LhdnStatusPollingJob(
        IServiceScopeFactory scopeFactory,
        ILogger<LhdnStatusPollingJob> logger,
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
                await PollSubmittedDocumentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in LhdnStatusPollingJob.");
            }

            await Task.Delay(_options.LhdnStatusPollingInterval, stoppingToken);
        }
    }

    /// <summary>One poll cycle (hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => PollSubmittedDocumentsAsync(ct);

    private async Task PollSubmittedDocumentsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LhdnDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<ILhdnGatewayAdapter>();
        var secretVault = scope.ServiceProvider.GetRequiredService<ISecretVault>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("LhdnEventBus");
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var linkService = scope.ServiceProvider.GetRequiredService<ILhdnLinkService>();

        var leaseUntil = DateTime.UtcNow.Add(_options.ClaimLeaseDuration);
        var submittedDocs = await ClaimSubmittedDocumentsAsync(db, leaseUntil, ct);
        if (submittedDocs.Count == 0) return;

        foreach (var doc in submittedDocs)
        {
            try
            {
                var config = await db.TenantConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.OrganizationId == doc.OrganizationId, ct);
                if (config == null || string.IsNullOrWhiteSpace(config.MyInvoisClientId) || string.IsNullOrWhiteSpace(config.MyInvoisClientSecret))
                {
                    continue;
                }

                var clientSecret = secretVault.DecryptOrPlaintext(config.MyInvoisClientSecret);
                var token = await gateway.GetTokenAsync(config.OrganizationId, config.MyInvoisClientId, clientSecret, config.IntermediaryMode, config.SupplierTin, ct, config.Environment);
                var result = await gateway.GetDocumentStatusAsync(config.MyInvoisClientId, token, doc.SubmissionUid!, config.IntermediaryMode, config.SupplierTin, ct);

                if (result.Success)
                {
                    if (result.Status == "VALID")
                    {
                        doc.MarkAsValid(result.LongId!, result.Uuid);

                        var portalUrl = linkService.GetPortalUrl(config.Environment);
                        var qrLink = (!string.IsNullOrEmpty(result.Uuid) && !string.IsNullOrEmpty(result.LongId))
                            ? $"{portalUrl}/{result.Uuid}/share/{result.LongId}"
                            : null;

                        await eventBus.PublishAsync(new LhdnDocumentValidatedIntegrationEvent(
                            doc.OrganizationId, doc.InternalReferenceId, result.Uuid!, "VALID", qrLink));

                        await mediator.Send(new DispatchExternalWebhookCommand(
                            doc.OrganizationId, doc.InternalReferenceId, "VALID", result.Uuid, result.LongId, null), ct);
                    }
                    else if (result.Status == "INVALID")
                    {
                        var errorMessage = result.ErrorMessage ?? "Validation failed at LHDN.";
                        doc.MarkAsInvalid(errorMessage);

                        await eventBus.PublishAsync(new LhdnDocumentValidatedIntegrationEvent(
                            doc.OrganizationId, doc.InternalReferenceId, result.Uuid ?? "", "INVALID", null));

                        await mediator.Send(new DispatchExternalWebhookCommand(
                            doc.OrganizationId, doc.InternalReferenceId, "INVALID", result.Uuid, null, errorMessage), ct);
                    }
                    else
                    {
                        doc.ScheduleNextPoll();
                    }
                }
                else
                {
                    doc.ScheduleNextPoll(result.RetryAfterSeconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll status for Document {DocId}", doc.Id);
                doc.ScheduleNextPoll();
            }
            finally
            {
                await db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// Claims SUBMITTED docs due for poll with FOR UPDATE SKIP LOCKED, leases via NextPollAt, commits before gateway I/O.
    /// </summary>
    internal static async Task<List<TaxDocument>> ClaimSubmittedDocumentsAsync(
        LhdnDbContext db,
        DateTime leaseUntilUtc,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        List<TaxDocument> submittedDocs;

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            const string sql = """
                SELECT * FROM lhdn."TaxDocuments"
                WHERE "ValidationStatus" = 'SUBMITTED'
                  AND "SubmissionUid" IS NOT NULL
                  AND ("NextPollAt" IS NULL OR "NextPollAt" <= NOW())
                ORDER BY "NextPollAt" NULLS FIRST
                LIMIT 50
                FOR UPDATE SKIP LOCKED;
                """;

            submittedDocs = await db.TaxDocuments
                .FromSqlRaw(sql)
                .IgnoreQueryFilters()
                .ToListAsync(ct);

            if (submittedDocs.Count == 0)
            {
                await transaction.RollbackAsync(ct);
                return submittedDocs;
            }

            foreach (var doc in submittedDocs)
            {
                doc.ClaimProcessingLease(leaseUntilUtc);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else
        {
            submittedDocs = await db.TaxDocuments
                .IgnoreQueryFilters()
                .Where(d => d.ValidationStatus == "SUBMITTED"
                    && d.SubmissionUid != null
                    && (d.NextPollAt == null || d.NextPollAt <= now))
                .OrderBy(d => d.NextPollAt)
                .Take(50)
                .ToListAsync(ct);

            if (submittedDocs.Count == 0) return submittedDocs;

            foreach (var doc in submittedDocs)
            {
                doc.ClaimProcessingLease(leaseUntilUtc);
            }

            await db.SaveChangesAsync(ct);
        }

        return submittedDocs;
    }
}
