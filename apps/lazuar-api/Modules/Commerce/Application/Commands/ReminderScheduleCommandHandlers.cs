using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application.Commands;

public class CreateReminderScheduleCommandHandler : ICommandHandler<CreateReminderScheduleCommand, Guid>
{
    private readonly ICommerceRepository _repository;

    public CreateReminderScheduleCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateReminderScheduleCommand request, CancellationToken ct)
    {
        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            var product = await _repository.GetProductByIdAsync(request.ProductId.Value, ct);
            if (product == null || product.OrganizationId != request.OrganizationId)
            {
                throw new InvalidOperationException("Product not found.");
            }
        }

        var schedule = new ReminderSchedule(
            request.OrganizationId,
            request.ProductId,
            request.TemplateId,
            request.Channel,
            request.DaysRelativeToDue,
            request.TimeOfDay,
            request.IsEnabled);

        _repository.AddReminderSchedule(schedule);
        await _repository.SaveChangesAsync(ct);

        return schedule.Id;
    }
}

public class UpdateReminderScheduleCommandHandler : ICommandHandler<UpdateReminderScheduleCommand>
{
    private readonly ICommerceRepository _repository;

    public UpdateReminderScheduleCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateReminderScheduleCommand request, CancellationToken ct)
    {
        var schedule = await _repository.GetReminderScheduleByIdAsync(request.ScheduleId, ct);
        if (schedule == null || schedule.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Reminder schedule not found.");

        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            var product = await _repository.GetProductByIdAsync(request.ProductId.Value, ct);
            if (product == null || product.OrganizationId != request.OrganizationId)
            {
                throw new InvalidOperationException("Product not found.");
            }
        }

        schedule.Update(
            request.ProductId ?? schedule.ProductId,
            request.TemplateId ?? schedule.TemplateId,
            request.Channel ?? schedule.Channel,
            request.DaysRelativeToDue ?? schedule.DaysRelativeToDue,
            request.TimeOfDay ?? schedule.TimeOfDay,
            request.IsEnabled ?? schedule.IsEnabled);

        await _repository.SaveChangesAsync(ct);
    }
}

public class DeleteReminderScheduleCommandHandler : ICommandHandler<DeleteReminderScheduleCommand>
{
    private readonly ICommerceRepository _repository;

    public DeleteReminderScheduleCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteReminderScheduleCommand request, CancellationToken ct)
    {
        var schedule = await _repository.GetReminderScheduleByIdAsync(request.ScheduleId, ct);
        if (schedule == null || schedule.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Reminder schedule not found.");

        _repository.RemoveReminderSchedule(schedule);
        await _repository.SaveChangesAsync(ct);
    }
}
