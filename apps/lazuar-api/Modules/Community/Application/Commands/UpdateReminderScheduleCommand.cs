using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

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

    public UpdateReminderScheduleCommandHandler(ICommunityReminderScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateReminderScheduleCommand request, CancellationToken ct)
    {
        var schedule = await _repository.GetByIdAsync(request.ScheduleId, ct);
        if (schedule == null || schedule.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Reminder schedule not found.");

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
