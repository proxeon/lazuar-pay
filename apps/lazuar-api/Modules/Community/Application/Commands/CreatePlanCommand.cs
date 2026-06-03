using BuildingBlocks.Application;
using Modules.Community.Domain.Aggregates;
using Modules.Community.Domain.ValueObjects;

namespace Modules.Community.Application.Commands;

public record CreatePlanCommand(
    Guid OrganizationId,
    string Slug,
    string Name,
    string Audience,
    string ShortDescription,
    string LongDescription,
    decimal Price,
    string Interval,
    int GracePeriodDays,
    int? MaxCapacity,
    int DisplayOrder,
    List<string> Features,
    string Methodology,
    List<FaqItemDto> Faq,
    string? TelegramInviteLink,
    string? WeeklyMeetingLink) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record FaqItemDto(string Id, string Question, string Answer);

public class CreatePlanCommandHandler : ICommandHandler<CreatePlanCommand, Guid>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunitySubscriptionRepository _uow;

    public CreatePlanCommandHandler(
        ICommunityPlanRepository planRepository, 
        ICommunitySubscriptionRepository uow)
    {
        _planRepository = planRepository;
        _uow = uow;
    }

    public async Task<Guid> Handle(CreatePlanCommand request, CancellationToken ct)
    {
        var isUnique = await _planRepository.IsSlugUniqueAsync(request.OrganizationId, request.Slug, ct);
        if (!isUnique)
        {
            throw new InvalidOperationException("The provided slug is already in use.");
        }

        var plan = new CommunityPlan(
            request.OrganizationId,
            request.Slug,
            request.Name,
            request.Audience,
            request.ShortDescription,
            request.LongDescription,
            request.Price,
            request.Interval,
            request.GracePeriodDays,
            request.MaxCapacity,
            request.DisplayOrder,
            request.Methodology);

        if (request.Features.Any())
            plan.UpdateFeatures(request.Features);

        if (request.Faq.Any())
        {
            var faqs = request.Faq.Select(f => new FaqItem(f.Id, f.Question, f.Answer)).ToList();
            plan.UpdateFaq(faqs);
        }

        plan.SetFulfillmentLinks(request.TelegramInviteLink, request.WeeklyMeetingLink);

        _planRepository.Add(plan);
        await _uow.SaveChangesAsync(ct);

        return plan.Id;
    }
}
