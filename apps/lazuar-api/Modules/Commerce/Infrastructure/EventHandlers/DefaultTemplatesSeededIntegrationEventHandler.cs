using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Domain.Aggregates;
using Modules.Communications.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class DefaultTemplatesSeededIntegrationEventHandler : IIntegrationEventHandler<DefaultTemplatesSeededIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public DefaultTemplatesSeededIntegrationEventHandler(
        CommerceDbContext dbContext,
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory sqlConnectionFactory)
    {
        _dbContext = dbContext;
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task HandleAsync(DefaultTemplatesSeededIntegrationEvent @event)
    {
        var hasSchedules = await _dbContext.ReminderSchedules
            .IgnoreQueryFilters()
            .AnyAsync(s => s.OrganizationId == @event.TenantId);

        if (hasSchedules) return;

        using var connection = _sqlConnectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string query = @"
            SELECT ""Id"", ""Name"" 
            FROM communications.""MessageTemplates"" 
            WHERE ""OrganizationId"" = @TenantId 
              AND ""Name"" IN ('Subscription Renewal (3 Days)', 'Subscription Renewal Due Today', 'Subscription Renewal Overdue')";

        var templates = await connection.QueryAsync<(Guid Id, string Name)>(query, new { TenantId = @event.TenantId });
        var templateDict = new Dictionary<string, Guid>();

        foreach (var t in templates)
        {
            templateDict[t.Name] = t.Id;
        }

        if (templateDict.TryGetValue("Subscription Renewal (3 Days)", out var preTemplateId) &&
            templateDict.TryGetValue("Subscription Renewal Due Today", out var dueTemplateId) &&
            templateDict.TryGetValue("Subscription Renewal Overdue", out var postTemplateId))
        {
            var defaultSchedules = new List<ReminderSchedule>
            {
                new ReminderSchedule(@event.TenantId, null, preTemplateId, "ALL", -3, "08:00", true),
                new ReminderSchedule(@event.TenantId, null, dueTemplateId, "ALL", 0, "08:00", true),
                new ReminderSchedule(@event.TenantId, null, postTemplateId, "ALL", 3, "08:00", true)
            };

            _dbContext.ReminderSchedules.AddRange(defaultSchedules);
            await _dbContext.SaveChangesAsync();
        }
    }
}
