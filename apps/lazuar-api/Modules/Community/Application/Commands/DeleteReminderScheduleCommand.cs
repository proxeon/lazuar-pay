using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record DeleteReminderScheduleCommand(Guid OrganizationId, Guid ScheduleId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class DeleteReminderScheduleCommandHandler : ICommandHandler<DeleteReminderScheduleCommand>
{
    private readonly ICommunityReminderScheduleRepository _repository;

    public DeleteReminderScheduleCommandHandler(ICommunityReminderScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteReminderScheduleCommand request, CancellationToken ct)
    {
        var schedule = await _repository.GetByIdAsync(request.ScheduleId, ct);
        if (schedule == null || schedule.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Reminder schedule not found.");

        _repository.Remove(schedule);

        await _repository.SaveChangesAsync(ct);
    }
}
