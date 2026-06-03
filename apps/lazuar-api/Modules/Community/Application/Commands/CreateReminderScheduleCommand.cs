using BuildingBlocks.Application;
using Modules.Community.Domain.Aggregates;

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

    public CreateReminderScheduleCommandHandler(ICommunityReminderScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateReminderScheduleCommand request, CancellationToken ct)
    {
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
