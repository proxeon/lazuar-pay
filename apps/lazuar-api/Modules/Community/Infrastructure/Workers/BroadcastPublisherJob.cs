using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Modules.Community.Infrastructure;
using Modules.Messaging.Contracts;

namespace Modules.Community.Infrastructure.Workers;

public class BroadcastPublisherJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BroadcastPublisherJob> _logger;
    private readonly DatabaseJobTrigger _jobTrigger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

    public BroadcastPublisherJob(
        IServiceScopeFactory scopeFactory,
        ILogger<BroadcastPublisherJob> logger,
        DatabaseJobTrigger jobTrigger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _jobTrigger = jobTrigger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Broadcast Publisher Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            int campaignsProcessed = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
                var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommunityEventBus");
                var sqlFactory = scope.ServiceProvider.GetRequiredKeyedService<ISqlConnectionFactory>("CommunitySqlConnectionFactory");

                var pendingCampaigns = await db.BroadcastCampaigns
                    .Where(c => c.Status == "PENDING")
                    .ToListAsync(stoppingToken);

                campaignsProcessed = pendingCampaigns.Count;

                foreach (var campaign in pendingCampaigns)
                {
                    try
                    {
                        campaign.MarkAsProcessing();
                        await db.SaveChangesAsync(stoppingToken);

                        using var connection = sqlFactory.CreateConnection();
                        connection.Open();

                        const string sql = @"
                            SELECT cp.""Email"", cp.""Phone""
                            FROM community.""Subscriptions"" s
                            JOIN crm.""ClientProfiles"" cp ON s.""ClientProfileId"" = cp.""Id""
                            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" = 'ACTIVE'
                            AND (@PlanId IS NULL OR s.""PlanId"" = @PlanId)";

                        var recipients = (await connection.QueryAsync<(string Email, string Phone)>(
                            sql,
                            new { OrgId = campaign.OrganizationId, PlanId = campaign.TargetPlanId })).ToList();

                        int total = 0;
                        foreach (var chunk in recipients.Chunk(100))
                        {
                            foreach (var recipient in chunk)
                            {
                                if (string.IsNullOrWhiteSpace(recipient.Email) && string.IsNullOrWhiteSpace(recipient.Phone))
                                    continue;

                                var evt = new DispatchMessageIntegrationEvent(
                                    campaign.OrganizationId,
                                    recipient.Email ?? "",
                                    recipient.Phone,
                                    campaign.Subject,
                                    campaign.Body,
                                    "ALL"
                                );
                                await eventBus.PublishAsync(evt);
                                total++;
                            }
                            await db.SaveChangesAsync(stoppingToken);
                        }

                        campaign.MarkAsCompleted(total);
                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Broadcast Campaign {CampaignId} completed. Sent to {Total} recipients.", campaign.Id, total);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process Broadcast Campaign {CampaignId}", campaign.Id);
                        campaign.MarkAsFailed(ex.Message);
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Broadcast Publisher worker.");
            }

            if (campaignsProcessed > 0)
            {
                await Task.Yield();
                continue;
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(_pollInterval);
                await _jobTrigger.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException) { }
        }
    }
}
