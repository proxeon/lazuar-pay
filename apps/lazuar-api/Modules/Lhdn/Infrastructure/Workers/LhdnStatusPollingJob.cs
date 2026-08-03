using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Lhdn.Infrastructure.Workers;

public class LhdnStatusPollingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LhdnStatusPollingJob> _logger;

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
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var linkService = scope.ServiceProvider.GetRequiredService<ILhdnLinkService>();

        var now = DateTime.UtcNow;

        var submittedDocs = await db.TaxDocuments
            .Where(d => d.ValidationStatus == "SUBMITTED" && d.SubmissionUid != null && (d.NextPollAt == null || d.NextPollAt <= now))
            .OrderBy(d => d.NextPollAt)
            .Take(50)
            .ToListAsync(ct);

        if (!submittedDocs.Any()) return;

        foreach (var doc in submittedDocs)
        {
            try
            {
                var config = await db.TenantConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.OrganizationId == doc.OrganizationId, ct);
                if (config == null || string.IsNullOrWhiteSpace(config.MyInvoisClientId) || string.IsNullOrWhiteSpace(config.MyInvoisClientSecret))
                {
                    continue;
                }

                var token = await gateway.GetTokenAsync(config.OrganizationId, config.MyInvoisClientId, config.MyInvoisClientSecret, config.IntermediaryMode, config.SupplierTin, ct);
                var result = await gateway.GetDocumentStatusAsync(config.MyInvoisClientId, token, doc.SubmissionUid!, config.IntermediaryMode, config.SupplierTin, ct);

                if (result.Success)
                {
                    if (result.Status == "VALID")
                    {
                        doc.MarkAsValid(result.LongId!);

                        var portalUrl = linkService.GetPortalUrl();
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
}
