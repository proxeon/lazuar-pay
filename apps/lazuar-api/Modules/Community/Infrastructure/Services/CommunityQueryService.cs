// apps/lazuar-api/Modules/Community/Infrastructure/Services/CommunityQueryService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly IOneQueryService _oneQueryService;

    public CommunityQueryService(
        [FromKeyedServices("CommunitySqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService,
        IOneQueryService oneQueryService)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
        _oneQueryService = oneQueryService;
    }

    public async Task<IEnumerable<PortalCommunitySpaceDto>> GetPortalSpacesAsync(Guid organizationId, IEnumerable<Guid> productIds)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();

        var ids = productIds.ToArray();
        if (ids.Length == 0) return Array.Empty<PortalCommunitySpaceDto>();

        const string sql = @"
            SELECT ""ProductId"" as product_id, ""Name"" as name, ""TelegramLink"" as telegram_link, ""ZoomLink"" as zoom_link
            FROM community.""CommunitySpaces""
            WHERE ""OrganizationId"" = @OrgId AND ""ProductId"" = ANY(@Ids)";

        return await Dapper.SqlMapper.QueryAsync<PortalCommunitySpaceDto>(connection, sql, new { OrgId = organizationId, Ids = ids });
    }
}
