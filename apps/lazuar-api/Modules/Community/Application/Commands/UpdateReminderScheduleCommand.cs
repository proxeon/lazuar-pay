// apps/lazuar-api/Modules/Community/Application/Commands/UpdateReminderScheduleCommand.cs
using BuildingBlocks.Application;
using Modules.Community.Application.Queries;

namespace Modules.Community.Application.Commands;

[AgentTool("Modify an existing global automated reminder schedule.", "COMMUNITY", "medium", "SUPER_ADMIN", "ADMIN")]
public record UpdateReminderScheduleCommand(
    Guid OrganizationId,
    Guid ScheduleId,
    Guid? PlanId,
    Guid? TemplateId,
    string? Channel,
    int? DaysRelativeToDue,
    string? TimeOfDay,
    bool? IsEnabled) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateReminderScheduleCommandHandler : ICommandHandler<UpdateReminderScheduleCommand>
{
    private readonly ICommunityReminderScheduleRepository _repository;
    private readonly ICommunityPlanRepository _planRepository;
    private readonly IMessageTemplateQueryService _templateService;

    public UpdateReminderScheduleCommandHandler(
        ICommunityReminderScheduleRepository repository,
        ICommunityPlanRepository planRepository,
        IMessageTemplateQueryService templateService)
    {
        _repository = repository;
        _planRepository = planRepository;
        _templateService = templateService;
    }

    public async Task Handle(UpdateReminderScheduleCommand request, CancellationToken ct)
    {
        var schedule = await _repository.GetByIdAsync(request.ScheduleId, ct);
        if (schedule == null || schedule.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Reminder schedule not found.");

        if (request.PlanId.HasValue && request.PlanId.Value != Guid.Empty)
        {
            var plan = await _planRepository.GetByIdAsync(request.PlanId.Value, ct);
            if (plan == null || plan.OrganizationId != request.OrganizationId)
            {
                throw new InvalidOperationException("Plan not found.");
            }
        }

        if (request.TemplateId.HasValue)
        {
            var templates = await _templateService.GetTemplatesAsync(new[] { request.TemplateId.Value });
            if (!templates.Any())
            {
                throw new InvalidOperationException("Template not found in Community module.");
            }
        }

        schedule.Update(
            request.PlanId ?? schedule.PlanId,
            request.TemplateId ?? schedule.TemplateId,
            request.Channel ?? schedule.Channel,
            request.DaysRelativeToDue ?? schedule.DaysRelativeToDue,
            request.TimeOfDay ?? schedule.TimeOfDay,
            request.IsEnabled ?? schedule.IsEnabled);

        await _repository.SaveChangesAsync(ct);
    }
}
