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

        // The templates are now guaranteed to be committed in the Communications schema
        // because we are reacting to the handshake event.
        using var connection = _sqlConnectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string query = @"
            SELECT ""Id"", ""Name"" 
            FROM communications.""MessageTemplates"" 
            WHERE ""OrganizationId"" = @TenantId 
              AND ""Name"" IN ('Community Renewal (3 Days)', 'Community Renewal Due Today', 'Community Renewal Overdue')";

        var templates = await connection.QueryAsync<(Guid Id, string Name)>(query, new { TenantId = @event.TenantId });
        var templateDict = new Dictionary<string, Guid>();

        foreach (var t in templates)
        {
            templateDict[t.Name] = t.Id;
        }

        if (templateDict.TryGetValue("Community Renewal (3 Days)", out var preTemplateId) &&
            templateDict.TryGetValue("Community Renewal Due Today", out var dueTemplateId) &&
            templateDict.TryGetValue("Community Renewal Overdue", out var postTemplateId))
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
