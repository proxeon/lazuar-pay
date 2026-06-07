using BuildingBlocks.Application;
using Modules.Community.Domain.Aggregates;
using Modules.Community.Application.Queries;

namespace Modules.Community.Application.Commands;

public record CreateReminderScheduleCommand(
    Guid OrganizationId,
    Guid? PlanId,
    Guid TemplateId,
    string Channel,
    int DaysRelativeToDue,
    string TimeOfDay,
    bool IsEnabled) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateReminderScheduleCommandHandler : ICommandHandler<CreateReminderScheduleCommand, Guid>
{
    private readonly ICommunityReminderScheduleRepository _repository;
    private readonly ICommunityPlanRepository _planRepository;
    private readonly IMessageTemplateQueryService _templateService;

    public CreateReminderScheduleCommandHandler(
        ICommunityReminderScheduleRepository repository,
        ICommunityPlanRepository planRepository,
        IMessageTemplateQueryService templateService)
    {
        _repository = repository;
        _planRepository = planRepository;
        _templateService = templateService;
    }

    public async Task<Guid> Handle(CreateReminderScheduleCommand request, CancellationToken ct)
    {
        // 1. Validate PlanId (if provided)
        if (request.PlanId.HasValue && request.PlanId.Value != Guid.Empty)
        {
            var plan = await _planRepository.GetByIdAsync(request.PlanId.Value, ct);
            if (plan == null || plan.OrganizationId != request.OrganizationId)
            {
                throw new InvalidOperationException("Plan not found.");
            }
        }

        // 2. Validate TemplateId within localized Community templates
        var templates = await _templateService.GetTemplatesAsync(new[] { request.TemplateId });
        if (!templates.Any())
        {
            throw new InvalidOperationException("Template not found in Community module.");
        }

        // 3. Construct and save the Aggregate
        var schedule = new CommunityReminderSchedule(
            request.OrganizationId,
            request.PlanId,
            request.TemplateId,
            request.Channel,
            request.DaysRelativeToDue,
            request.TimeOfDay,
            request.IsEnabled);

        _repository.Add(schedule);
        await _repository.SaveChangesAsync(ct);

        return schedule.Id;
    }
}
