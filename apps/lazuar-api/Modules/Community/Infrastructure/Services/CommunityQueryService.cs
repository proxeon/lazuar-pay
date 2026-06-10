using Dapper;
using System.Data;
using System.Text.Json;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application.Queries;
using Modules.CRM.Contracts;
using Lazuar.ApiTypes;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService : ICommunityQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IMessageTemplateQueryService _messageTemplateQueryService;

    public CommunityQueryService(
        [FromKeyedServices("CommunitySqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService,
        IMessageTemplateQueryService messageTemplateQueryService)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
        _messageTemplateQueryService = messageTemplateQueryService;
    }

    private static CommunityPlanDto MapToPlanDto(RawPlanDto raw, int enrolledCount)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
        var features = string.IsNullOrEmpty(raw.Features) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(raw.Features, options) ?? new List<string>();
        var faqList = string.IsNullOrEmpty(raw.Faq) ? new List<Lazuar.ApiTypes.FaqItemDto>() : JsonSerializer.Deserialize<List<Lazuar.ApiTypes.FaqItemDto>>(raw.Faq, options) ?? new List<Lazuar.ApiTypes.FaqItemDto>();
        var spotsRemaining = raw.MaxCapacity.HasValue ? Math.Max(0, raw.MaxCapacity.Value - enrolledCount) : (int?)null;
        var isFull = raw.MaxCapacity.HasValue && enrolledCount >= raw.MaxCapacity.Value;
        
        return new CommunityPlanDto
        {
            Id = raw.Id.ToString(),
            Slug = raw.Slug,
            Name = raw.Name,
            Audience = raw.Audience,
            Short_description = raw.ShortDescription,
            Long_description = raw.LongDescription ?? "",
            Price = (double)raw.Price,
            Interval = raw.Interval,
            Features = features,
            Methodology = raw.Methodology ?? "",
            Faq = faqList,
            Is_active = raw.IsActive,
            Display_order = raw.DisplayOrder,
            Max_capacity = raw.MaxCapacity,
            Grace_period_days = raw.GracePeriodDays,
            Telegram_invite_link = raw.TelegramInviteLink,
            Weekly_meeting_link = raw.WeeklyMeetingLink,
            Enrolled_count = enrolledCount,
            Spots_remaining = spotsRemaining,
            Is_full = isFull
        };
    }
}
