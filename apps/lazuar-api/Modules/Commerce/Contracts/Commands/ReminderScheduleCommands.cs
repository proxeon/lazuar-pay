using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record CreateReminderScheduleCommand(
    Guid OrganizationId,
    Guid? ProductId,
    Guid TemplateId,
    string Channel,
    int DaysRelativeToDue,
    string TimeOfDay,
    bool IsEnabled) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record UpdateReminderScheduleCommand(
    Guid OrganizationId,
    Guid ScheduleId,
    Guid? ProductId,
    Guid? TemplateId,
    string? Channel,
    int? DaysRelativeToDue,
    string? TimeOfDay,
    bool? IsEnabled) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record DeleteReminderScheduleCommand(Guid OrganizationId, Guid ScheduleId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
