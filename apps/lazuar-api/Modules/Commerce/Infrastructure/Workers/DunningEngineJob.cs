using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Commerce.Domain;

namespace Modules.Commerce.Infrastructure.Workers;

/// <summary>
/// Hourly dunning engine: claim subscriptions, run pre-dunning reminders and past-due steps.
/// Logic is split across partials (Claim, PreDunning, PastDue, Dispatch) for readability.
/// </summary>
public partial class DunningEngineJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DunningEngineJob> _logger;
    private readonly BackgroundWorkerOptions _options;
    private readonly int _batchSize;

    public DunningEngineJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DunningEngineJob> logger,
        IOptions<BackgroundWorkerOptions> options)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _options = options.Value;
        _batchSize = Math.Clamp(_options.DunningEngineBatchSize, 1, 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dunning Engine Job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDunningAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the dunning engine.");
            }

            await Task.Delay(_options.DunningEngineInterval, stoppingToken);
        }
    }

    /// <summary>One engine cycle (hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => ProcessDunningAsync(ct);

    private async Task ProcessDunningAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var campaigns = await db.DunningCampaigns
            .IgnoreQueryFilters()
            .Include(c => c.Steps)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.PriorityOrder)
            .ThenByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var whatsAppEnabled = _configuration.GetValue("Messaging:WhatsAppEnabled", false);

        await ProcessClaimedBatchAsync(
            ClaimMode.PreDunning,
            campaigns,
            whatsAppEnabled,
            ct);

        await ProcessClaimedBatchAsync(
            ClaimMode.PastDue,
            campaigns,
            whatsAppEnabled,
            ct);
    }
}
