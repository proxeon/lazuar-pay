using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application.Commands;

public class CreateDunningCampaignCommandHandler : ICommandHandler<CreateDunningCampaignCommand, Guid>
{
    private readonly ICommerceRepository _repository;

    public CreateDunningCampaignCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateDunningCampaignCommand request, CancellationToken ct)
    {
        var campaign = new DunningCampaign(
            request.OrganizationId,
            request.Name,
            request.FinalAction,
            request.GracePeriodDays,
            request.TargetProductIds,
            request.TargetPaymentMethods);

        foreach (var step in request.Steps)
        {
            campaign.AddStep(step.DayOffset, step.TemplateId, step.Channel);
        }

        _repository.AddDunningCampaign(campaign);
        await _repository.SaveChangesAsync(ct);

        return campaign.Id;
    }
}

public class UpdateDunningCampaignCommandHandler : ICommandHandler<UpdateDunningCampaignCommand>
{
    private readonly ICommerceRepository _repository;

    public UpdateDunningCampaignCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateDunningCampaignCommand request, CancellationToken ct)
    {
        var campaign = await _repository.GetDunningCampaignByIdAsync(request.OrganizationId, request.CampaignId, ct);
        if (campaign == null) throw new InvalidOperationException("Dunning campaign not found.");

        campaign.UpdateDetails(
            request.Name,
            request.FinalAction,
            request.GracePeriodDays,
            request.TargetProductIds,
            request.TargetPaymentMethods);

        campaign.ClearSteps();
        foreach (var step in request.Steps)
        {
            campaign.AddStep(step.DayOffset, step.TemplateId, step.Channel);
        }

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value) campaign.Restore();
            else campaign.Archive();
        }

        await _repository.SaveChangesAsync(ct);
    }
}

public class DeleteDunningCampaignCommandHandler : ICommandHandler<DeleteDunningCampaignCommand>
{
    private readonly ICommerceRepository _repository;

    public DeleteDunningCampaignCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteDunningCampaignCommand request, CancellationToken ct)
    {
        var campaign = await _repository.GetDunningCampaignByIdAsync(request.OrganizationId, request.CampaignId, ct);
        if (campaign == null) throw new InvalidOperationException("Dunning campaign not found.");

        _repository.RemoveDunningCampaign(campaign);
        await _repository.SaveChangesAsync(ct);
    }
}

public class GenerateDefaultDunningCampaignsCommandHandler : ICommandHandler<GenerateDefaultDunningCampaignsCommand>
{
    private readonly ICommerceRepository _repository;

    public GenerateDefaultDunningCampaignsCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(GenerateDefaultDunningCampaignsCommand request, CancellationToken ct)
    {
        var templateDict = await _repository.GetDefaultTemplateIdsAsync(request.OrganizationId, ct);

        var campaign = new DunningCampaign(
            request.OrganizationId,
            "Standard Recovery Strategy",
            "CANCEL",
            3);

        if (templateDict.TryGetValue("Subscription Renewal (3 Days)", out var preTemplateId))
            campaign.AddStep(-3, preTemplateId, "ALL");

        if (templateDict.TryGetValue("Subscription Renewal Due Today", out var dueTemplateId))
            campaign.AddStep(0, dueTemplateId, "ALL");

        if (templateDict.TryGetValue("Subscription Renewal Overdue", out var postTemplateId))
            campaign.AddStep(3, postTemplateId, "ALL");

        _repository.AddDunningCampaign(campaign);
        await _repository.SaveChangesAsync(ct);
    }
}
