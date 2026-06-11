using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("List all automated reminder schedules to find a ScheduleId, TemplateId, or current configuration.", "low", "SUPER_ADMIN", "ADMIN")]
public record ListReminderSchedulesAgentQuery(Guid OrganizationId) : IQuery<IEnumerable<AgentReminderScheduleResult>>;

public record AgentReminderScheduleResult(
    string ScheduleId,
    string? PlanName,
    string TemplateName,
    string Channel,
    int DaysRelativeToDue,
    string TimeOfDay,
    bool IsEnabled);

public class ListReminderSchedulesAgentQueryHandler : IQueryHandler<ListReminderSchedulesAgentQuery, IEnumerable<AgentReminderScheduleResult>>
{
    private readonly ICommunityQueryService _queryService;

    public ListReminderSchedulesAgentQueryHandler(ICommunityQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<IEnumerable<AgentReminderScheduleResult>> Handle(ListReminderSchedulesAgentQuery request, CancellationToken cancellationToken)
    {
        var schedules = await _queryService.GetReminderSchedulesAsync(request.OrganizationId);

        return schedules.Select(s => new AgentReminderScheduleResult(
            s.Id,
            s.Plan_name,
            s.Template_name,
            s.Channel,
            s.Days_relative_to_due,
            s.Time_of_day,
            s.Is_enabled
        )).ToList();
    }
}
