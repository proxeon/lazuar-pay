using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Contracts.Events;

namespace Modules.Lhdn.Infrastructure.Workers;

public class LhdnStatusPollingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LhdnStatusPollingJob> _logger;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _throttleSemaphore = new(1, 1);
    private readonly TimeSpan _delayBetweenRequests = TimeSpan.FromMilliseconds(250); 

    public LhdnStatusPollingJob(IServiceScopeFactory scopeFactory, ILogger<LhdnStatusPollingJob> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
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

        var submittedDocs = await db.TaxDocuments
            .Where(d => d.ValidationStatus == "SUBMITTED" && d.SubmissionUid != null)
            .OrderBy(d => d.UpdatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (!submittedDocs.Any()) return;

        var clientId = _configuration["Lhdn:ClientId"] ?? throw new InvalidOperationException("LHDN ClientId missing.");
        var clientSecret = _configuration["Lhdn:ClientSecret"] ?? throw new InvalidOperationException("LHDN ClientSecret missing.");

        foreach (var doc in submittedDocs)
        {
            await _throttleSemaphore.WaitAsync(ct);
            try
            {
                var config = await db.TenantConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.OrganizationId == doc.OrganizationId, ct);
                if (config == null) continue;

                var token = await gateway.GetTokenAsync(config.OrganizationId, clientId, clientSecret, config.IntermediaryMode, null, ct);
                var result = await gateway.GetDocumentStatusAsync(token, doc.SubmissionUid!, ct);

                if (result.Success)
                {
                    if (result.Status == "VALID")
                    {
                        doc.MarkAsValid(result.LongId!);
                        
                        await eventBus.PublishAsync(new LhdnDocumentValidatedIntegrationEvent(
                            doc.OrganizationId, doc.InternalReferenceId, result.Uuid!, result.LongId!, "VALID"));

                        await mediator.Send(new DispatchExternalWebhookCommand(
                            doc.OrganizationId, doc.InternalReferenceId, "VALID", result.Uuid, result.LongId, null), ct);
                    }
                    else if (result.Status == "INVALID")
                    {
                        // Use the extracted error message from the details API
                        var errorMessage = result.ErrorMessage ?? "Validation failed at LHDN.";
                        doc.MarkAsInvalid(errorMessage);

                        await mediator.Send(new DispatchExternalWebhookCommand(
                            doc.OrganizationId, doc.InternalReferenceId, "INVALID", result.Uuid, null, errorMessage), ct);
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
