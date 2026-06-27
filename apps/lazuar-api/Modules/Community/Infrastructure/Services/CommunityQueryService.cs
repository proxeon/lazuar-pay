// apps/lazuar-api/Modules/Community/Infrastructure/Services/CommunityQueryService.cs
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application.Queries;
using Modules.CRM.Contracts;
using Modules.One.Contracts;

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
}
