using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application.Commands;

public class GenerateDefaultSchedulesCommandHandler : ICommandHandler<GenerateDefaultSchedulesCommand>
{
    private readonly ICommerceRepository _repository;

    public GenerateDefaultSchedulesCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(GenerateDefaultSchedulesCommand request, CancellationToken ct)
    {
        var templateDict = await _repository.GetDefaultTemplateIdsAsync(request.OrganizationId, ct);

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
