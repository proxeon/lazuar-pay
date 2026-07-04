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
            request.PriorityOrder,
            request.TargetProductIds,
            request.TargetPaymentMethods);

        foreach (var step in request.Steps)
        {
            campaign.AddStep(step.DayOffset, step.ActionType, step.Subject, step.EmailBody, step.WhatsAppBody);
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
            request.PriorityOrder,
            request.TargetProductIds,
            request.TargetPaymentMethods);

        campaign.ClearSteps();
        foreach (var step in request.Steps)
        {
            campaign.AddStep(step.DayOffset, step.ActionType, step.Subject, step.EmailBody, step.WhatsAppBody);
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
        var campaign = new DunningCampaign(
            request.OrganizationId,
            "Standard Recovery Strategy",
            "CANCEL",
            7, 
            0);

        campaign.AddStep(-3, "EMAIL", 
            "Upcoming renewal for {{plan_name}}", 
            "Your {{plan_name}} subscription will renew in 3 days. Manage your account here: {{renewal_link}}", 
            null);
            
        campaign.AddStep(0, "EMAIL", 
            "Action Required: {{plan_name}} renewal due today", 
            "Your {{plan_name}} subscription is due today. Renew here: {{renewal_link}}", 
            null);
            
        campaign.AddStep(3, "WHATSAPP", 
            null, 
            null, 
            "Hey {{customer_name}}, your {{plan_name}} subscription is past due. Renew here: {{renewal_link}}");

        _repository.AddDunningCampaign(campaign);
        await _repository.SaveChangesAsync(ct);
    }
}
