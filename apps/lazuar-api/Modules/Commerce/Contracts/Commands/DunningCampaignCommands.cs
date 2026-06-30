using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record DunningStepData(int DayOffset, Guid TemplateId, string Channel);

public record CreateDunningCampaignCommand(
    Guid OrganizationId,
    string Name,
    string FinalAction,
    int GracePeriodDays,
    List<Guid>? TargetProductIds,
    List<string>? TargetPaymentMethods,
    List<DunningStepData> Steps) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record UpdateDunningCampaignCommand(
    Guid OrganizationId,
    Guid CampaignId,
    string Name,
    string FinalAction,
    int GracePeriodDays,
    List<Guid>? TargetProductIds,
    List<string>? TargetPaymentMethods,
    List<DunningStepData> Steps,
    bool? IsActive) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record DeleteDunningCampaignCommand(Guid OrganizationId, Guid CampaignId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record GenerateDefaultDunningCampaignsCommand(Guid OrganizationId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
