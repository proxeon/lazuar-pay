// apps/lazuar-api/Modules/Community/Application/Commands/UpdatePlanCommand.cs
using BuildingBlocks.Application;
using Modules.Community.Domain.ValueObjects;

namespace Modules.Community.Application.Commands;

[AgentTool("Modify pricing, descriptions, or capacity limits of an existing plan.", "medium", "SUPER_ADMIN", "ADMIN")]
public record UpdatePlanCommand(
    Guid OrganizationId,
    Guid PlanId,
    string? Slug,
    string? Name,
    string? Audience,
    string? ShortDescription,
    string? LongDescription,
    decimal? Price,
    string? Interval,
    List<string>? Features,
    string? Methodology,
    List<FaqItemDto>? Faq,
    bool? IsActive,
    int? DisplayOrder,
    int? MaxCapacity,
    int? GracePeriodDays,
    string? TelegramInviteLink,
    string? WeeklyMeetingLink) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdatePlanCommandHandler : ICommandHandler<UpdatePlanCommand>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunitySubscriptionRepository _uow;

    public UpdatePlanCommandHandler(ICommunityPlanRepository planRepository, ICommunitySubscriptionRepository uow)
    {
        _planRepository = planRepository;
        _uow = uow;
    }

    public async Task Handle(UpdatePlanCommand request, CancellationToken ct)
    {
        var plan = await _planRepository.GetByIdAsync(request.PlanId, ct);
        if (plan == null || plan.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Plan not found.");

        if (request.Slug != null && request.Slug != plan.Slug)
        {
            var isUnique = await _planRepository.IsSlugUniqueAsync(request.OrganizationId, request.Slug, ct);
            if (!isUnique) throw new InvalidOperationException("The provided slug is already in use.");
            plan.SetSlug(request.Slug);
        }

        plan.UpdateDetails(
            request.Name ?? plan.Name,
            request.Audience ?? plan.Audience,
            request.ShortDescription ?? plan.ShortDescription,
            request.LongDescription ?? plan.LongDescription,
            request.Price ?? plan.Price,
            request.Interval ?? plan.Interval,
            request.GracePeriodDays ?? plan.GracePeriodDays,
            request.MaxCapacity,
            request.DisplayOrder ?? plan.DisplayOrder,
            request.IsActive ?? plan.IsActive,
            request.Methodology ?? plan.Methodology
        );

        if (request.Features != null)
        {
            plan.UpdateFeatures(request.Features);
        }

        if (request.Faq != null)
        {
            var faqs = request.Faq.Select(f => new FaqItem(f.Id, f.Question, f.Answer)).ToList();
            plan.UpdateFaq(faqs);
        }

        if (request.TelegramInviteLink != null || request.WeeklyMeetingLink != null)
        {
            plan.SetFulfillmentLinks(
                request.TelegramInviteLink ?? plan.TelegramInviteLink, 
                request.WeeklyMeetingLink ?? plan.WeeklyMeetingLink);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
