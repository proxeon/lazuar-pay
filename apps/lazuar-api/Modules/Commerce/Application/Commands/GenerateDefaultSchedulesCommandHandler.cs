using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application.Commands;

public class GenerateDefaultSchedulesCommandHandler : ICommandHandler<GenerateDefaultSchedulesCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GenerateDefaultSchedulesCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory sqlConnectionFactory)
    {
        _repository = repository;
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task Handle(GenerateDefaultSchedulesCommand request, CancellationToken ct)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string query = @"
            SELECT ""Id"", ""Name"" 
            FROM communications.""MessageTemplates"" 
            WHERE ""OrganizationId"" = @TenantId 
              AND ""Name"" IN ('Subscription Renewal (3 Days)', 'Subscription Renewal Due Today', 'Subscription Renewal Overdue')";

        var templates = await connection.QueryAsync<(Guid Id, string Name)>(query, new { TenantId = request.OrganizationId });
        var templateDict = new Dictionary<string, Guid>();

        foreach (var t in templates)
        {
            templateDict[t.Name] = t.Id;
        }

        if (templateDict.TryGetValue("Subscription Renewal (3 Days)", out var preTemplateId))
        {
            _repository.AddReminderSchedule(new ReminderSchedule(request.OrganizationId, null, preTemplateId, "ALL", -3, "08:00", true));
        }

        if (templateDict.TryGetValue("Subscription Renewal Due Today", out var dueTemplateId))
        {
            _repository.AddReminderSchedule(new ReminderSchedule(request.OrganizationId, null, dueTemplateId, "ALL", 0, "08:00", true));
        }

        if (templateDict.TryGetValue("Subscription Renewal Overdue", out var postTemplateId))
        {
            _repository.AddReminderSchedule(new ReminderSchedule(request.OrganizationId, null, postTemplateId, "ALL", 3, "08:00", true));
        }

        await _repository.SaveChangesAsync(ct);
    }
}
