using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Lhdn.Infrastructure.Workers;

/// <summary>
/// Polls LHDN for the status of SUBMITTED documents.
/// Throttled to ensure compliance with the 300 RPM limit.
/// Publishes integration events upon successful validation.
/// </summary>
public class LhdnStatusPollingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LhdnStatusPollingJob> _logger;
    private readonly SemaphoreSlim _throttleSemaphore = new(1, 1);
    private readonly TimeSpan _delayBetweenRequests = TimeSpan.FromMilliseconds(250); 

    public LhdnStatusPollingJob(IServiceScopeFactory scopeFactory, ILogger<LhdnStatusPollingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task PollSubmittedDocumentsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LhdnDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<ILhdnGatewayAdapter>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("LhdnEventBus");

        var submittedDocs = await db.TaxDocuments
            .Where(d => d.ValidationStatus == "SUBMITTED" && d.SubmissionUid != null)
            .OrderBy(d => d.UpdatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (!submittedDocs.Any()) return;

        foreach (var doc in submittedDocs)
        {
            await _throttleSemaphore.WaitAsync(ct);
            try
            {
                var config = await db.TenantConfigs.FirstOrDefaultAsync(c => c.OrganizationId == doc.OrganizationId, ct);
                if (config == null) continue;

                var token = await gateway.GetTokenAsync(config.OrganizationId, "clientId_todo", "clientSecret_todo", config.IntermediaryMode, null, ct);
                var result = await gateway.GetDocumentStatusAsync(token, doc.SubmissionUid!, ct);

                if (result.Success)
                {
                    if (result.Status == "VALID")
                    {
                        doc.MarkAsValid(result.LongId!);
                        
                        var integrationEvent = new LhdnDocumentValidatedIntegrationEvent(
                            doc.OrganizationId,
                            doc.InternalReferenceId,
                            result.Uuid!,
                            result.LongId!,
                            "VALID"
                        );
                        
                        await eventBus.PublishAsync(integrationEvent);
                    }
                    else if (result.Status == "INVALID")
                    {
                        doc.MarkAsInvalid("Validation failed at LHDN.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll status for Document {DocId}", doc.Id);
            }
            finally
            {
                await db.SaveChangesAsync(ct);
                _throttleSemaphore.Release();
                await Task.Delay(_delayBetweenRequests, ct);
            }
        }
    }
}
