using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    private record RawCommunitySpaceDto(string ProductIdsJson, string Name, string? TelegramLink, string? ZoomLink);

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
            SELECT ""ProductIds""::text as ProductIdsJson, ""Name"" as name, ""TelegramLink"" as telegram_link, ""ZoomLink"" as zoom_link
            FROM community.""CommunitySpaces""
            WHERE ""OrganizationId"" = @OrgId";

        var rawSpaces = await Dapper.SqlMapper.QueryAsync<RawCommunitySpaceDto>(connection, sql, new { OrgId = organizationId });

        var results = new List<PortalCommunitySpaceDto>();

        foreach (var space in rawSpaces)
        {
            List<Guid> parsedIds = new();
            if (!string.IsNullOrWhiteSpace(space.ProductIdsJson))
            {
                try
                {
                    parsedIds = JsonSerializer.Deserialize<List<Guid>>(space.ProductIdsJson) ?? new List<Guid>();
                }
                catch { }
            }

            if (parsedIds.Any(id => ids.Contains(id)))
            {
                results.Add(new PortalCommunitySpaceDto
                {
                    Product_ids = parsedIds.Select(id => id.ToString()).ToList(),
                    Name = space.Name,
                    Telegram_link = space.TelegramLink,
                    Zoom_link = space.ZoomLink
                });
            }
        }

        return results;
    }
}
