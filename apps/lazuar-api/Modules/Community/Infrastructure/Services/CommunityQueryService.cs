// apps/lazuar-api/Modules/Community/Infrastructure/Services/CommunityQueryService.cs
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application.Queries;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Lazuar.ApiTypes;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService : ICommunityQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IMessageTemplateQueryService _messageTemplateQueryService;
    private readonly IOneQueryService _oneQueryService;

    public CommunityQueryService(
        [FromKeyedServices("CommunitySqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService,
        IMessageTemplateQueryService messageTemplateQueryService,
        IOneQueryService oneQueryService)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
        _messageTemplateQueryService = messageTemplateQueryService;
        _oneQueryService = oneQueryService;
    }

    private static CommunityPlanDto MapToPlanDto(RawPlanDto raw, int enrolledCount)
    {
        var spotsRemaining = raw.MaxCapacity.HasValue ? Math.Max(0, raw.MaxCapacity.Value - enrolledCount) : (int?)null;
        var isFull = raw.MaxCapacity.HasValue && enrolledCount >= raw.MaxCapacity.Value;

        return new CommunityPlanDto
        {
            Id = raw.Id.ToString(),
            Slug = raw.Slug,
            Name = raw.Name,
            Audience = raw.Audience,
            Price = (double)raw.Price,
            Interval = raw.Interval,
            Admin_notes = raw.AdminNotes,
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
