using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.One.Contracts;

namespace Modules.Commerce.Infrastructure.Workers;

/// <summary>
/// Hourly AR reminders for OPEN custom (quote) checkout sessions at DueAt offsets -3 / 0 / +3.
/// Does not mark the session PAST_DUE.
/// </summary>
public class InvoiceReminderJob : BackgroundService
{
    public static readonly int[] Offsets = [-3, 0, 3];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceReminderJob> _logger;

    public InvoiceReminderJob(
        IServiceScopeFactory scopeFactory,
        ILogger<InvoiceReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Invoice reminder job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invoice reminder job failed.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");
        var one = scope.ServiceProvider.GetRequiredService<IOneQueryService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var today = DateTime.UtcNow.Date;
        var sessions = await db.CheckoutSessions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "OPEN"
                && s.ProductId == null
                && s.DueAt != null)
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            return;
        }

        var portalBase = BuildingBlocks.Infrastructure.AppClientUrl.Resolve(config);
        var sentCount = 0;

        foreach (var session in sessions)
        {
            var dueDate = session.DueAt!.Value.Date;
            var dayOffset = (today - dueDate).Days;
            if (!Offsets.Contains(dayOffset))
            {
                continue;
            }

            var already = await db.InvoiceReminderDispatchLogs
                .AnyAsync(l => l.SessionId == session.Id && l.DayOffset == dayOffset, ct);
            if (already)
            {
                continue;
            }

            var workspace = await one.GetWorkspaceByIdAsync(session.OrganizationId);
            var slug = workspace?.Slug?.Trim() ?? "";
            if (string.IsNullOrEmpty(slug))
            {
                _logger.LogWarning(
                    "Skipping invoice reminder for session {SessionId}: workspace slug missing.",
                    session.Id);
                continue;
            }

            var payUrl = $"{portalBase}/{slug}/pay/{session.Id}";

            var total = session.AdHocLineItems.Sum(i => i.Quantity * i.UnitPrice);
            var payloadObj = new
            {
                client_profile_id = session.ClientProfileId.ToString(),
                session_id = session.Id.ToString(),
                document_number = session.DocumentNumber ?? string.Empty,
                checkout_url = payUrl,
                due_at = session.DueAt.Value.ToString("yyyy-MM-dd"),
                amount = total,
                total_price = total,
                currency = "MYR",
                day_offset = dayOffset,
                plan_name = session.DocumentNumber ?? "Quote"
            };

            var payloadElement = JsonSerializer.SerializeToElement(
                payloadObj,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            db.InvoiceReminderDispatchLogs.Add(new Domain.Entities.InvoiceReminderDispatchLog(session.Id, dayOffset));
            await eventBus.PublishAsync(new FulfillmentRequestedIntegrationEvent(
                session.OrganizationId,
                "COMMUNICATIONS",
                "invoice.reminder",
                payloadElement));

            try
            {
                await db.SaveChangesAsync(ct);
                sentCount++;
            }
            catch (DbUpdateException)
            {
                // Unique (SessionId, DayOffset): another replica already claimed this offset.
                db.ChangeTracker.Clear();
            }
        }

        if (sentCount > 0)
        {
            _logger.LogInformation("Dispatched {Count} invoice reminder(s).", sentCount);
        }
    }
}
