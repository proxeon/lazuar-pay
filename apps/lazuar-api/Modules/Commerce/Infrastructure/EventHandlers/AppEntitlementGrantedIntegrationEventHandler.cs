using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Domain.Aggregates;
using Modules.One.Contracts;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class AppEntitlementGrantedIntegrationEventHandler : IIntegrationEventHandler<AppEntitlementGrantedIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public AppEntitlementGrantedIntegrationEventHandler(
        CommerceDbContext dbContext,
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory sqlConnectionFactory)
    {
        _dbContext = dbContext;
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task HandleAsync(AppEntitlementGrantedIntegrationEvent @event)
    {
        // Commerce relies on the core billing/payments infrastructure, but specifically needs to seed its 
        // own dunning lifecycle rules when a tenant provisions the platform.
        if (@event.AppId != "COMMUNITY" && @event.AppId != "COMMERCE" && @event.AppId != "BILLING") 
            return;

        var hasSchedules = await _dbContext.ReminderSchedules
            .IgnoreQueryFilters()
            .AnyAsync(s => s.OrganizationId == @event.TenantId);

        if (hasSchedules) return;

        // Since templates are managed by the Communications module (in a different schema),
        // we use a direct Dapper query to securely resolve the seeded Template IDs without
        // violating EF Core boundaries or cross-schema constraints.
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
