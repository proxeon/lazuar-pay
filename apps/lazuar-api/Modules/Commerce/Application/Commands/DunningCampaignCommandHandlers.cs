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

        if (await _repository.HasSubscriptionsAssignedToCampaignAsync(request.CampaignId, ct))
        {
            throw new InvalidOperationException(
                "Cannot delete a dunning campaign while subscriptions are assigned to it. Archive the campaign instead.");
        }

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
        // Idempotent seed: never create unbounded duplicate defaults.
        if (await _repository.HasAnyDunningCampaignAsync(request.OrganizationId, ct))
        {
            return;
        }

        var campaign = new DunningCampaign(
            request.OrganizationId,
            "Standard Recovery Strategy",
            "CANCEL",
            7, 
            0);

        campaign.AddStep(-3, "EMAIL", 
            "Upcoming renewal for {{plan_name}}", 
            "Your {{plan_name}} subscription will renew in 3 days. Ensure your payment method is up to date here: {{update_payment_link}}", 
            null);
            
        campaign.AddStep(0, "EMAIL", 
            "Action Required: {{plan_name}} renewal due today", 
            "Your {{plan_name}} subscription is due today. To maintain access, please update your payment method here: {{update_payment_link}}", 
            null);
            
        campaign.AddStep(3, "WHATSAPP", 
            null, 
            null, 
            "Hey {{customer_name}}, your {{plan_name}} subscription is past due. You can securely update your payment method to restore access here: {{update_payment_link}}");

        _repository.AddDunningCampaign(campaign);
        await _repository.SaveChangesAsync(ct);
    }
}
