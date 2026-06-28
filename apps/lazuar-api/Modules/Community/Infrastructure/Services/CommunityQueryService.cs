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

    private class RawCommunitySpaceDto
    {
        public string ProductIdsJson { get; set; } = "";
        public string Name { get; set; } = "";
        public string? TelegramLink { get; set; }
        public string? ZoomLink { get; set; }
    }

    private class RawAdminCommunitySpaceDto
    {
        public Guid Id { get; set; }
        public string ProductIdsJson { get; set; } = "";
        public string Name { get; set; } = "";
        public string? TelegramLink { get; set; }
        public string? ZoomLink { get; set; }
        public string LinkedCheckoutsJson { get; set; } = "[]";
    }

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
            SELECT ""ProductIds""::text as ProductIdsJson, ""Name"" as Name, ""TelegramLink"" as TelegramLink, ""ZoomLink"" as ZoomLink
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

    public async Task<IEnumerable<AdminCommunitySpaceDto>> GetAdminSpacesAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();

        const string sql = @"
            WITH SpaceProducts AS (
                SELECT s.""Id"", jsonb_array_elements_text(s.""ProductIds"")::uuid AS ""ProductId""
                FROM community.""CommunitySpaces"" s
                WHERE s.""OrganizationId"" = @OrgId
            ),
            LinkedData AS (
                SELECT 
                    sp.""Id"",
                    jsonb_agg(
                        jsonb_build_object(
                            'id', p.""Id"",
                            'name', p.""Name"",
                            'slug', p.""Slug""
                        )
                    ) as LinkedCheckouts
                FROM SpaceProducts sp
                JOIN commerce.""Products"" p ON sp.""ProductId"" = p.""Id""
                GROUP BY sp.""Id""
            )
            SELECT 
                s.""Id"", 
                s.""ProductIds""::text as ProductIdsJson, 
                s.""Name"" as Name, 
                s.""TelegramLink"" as TelegramLink, 
                s.""ZoomLink"" as ZoomLink,
                COALESCE(ld.LinkedCheckouts::text, '[]') as LinkedCheckoutsJson
            FROM community.""CommunitySpaces"" s
            LEFT JOIN LinkedData ld ON s.""Id"" = ld.""Id""
            WHERE s.""OrganizationId"" = @OrgId
            ORDER BY s.""Name""";

        var rawSpaces = await Dapper.SqlMapper.QueryAsync<RawAdminCommunitySpaceDto>(connection, sql, new { OrgId = organizationId });

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        return rawSpaces.Select(space => 
        {
            List<Guid> parsedIds = new();
            if (!string.IsNullOrWhiteSpace(space.ProductIdsJson))
            {
                try
                {
                    parsedIds = JsonSerializer.Deserialize<List<Guid>>(space.ProductIdsJson, jsonOptions) ?? new List<Guid>();
                }
                catch { }
            }

            List<LinkedCheckoutDto> linkedCheckouts = new();
            if (!string.IsNullOrWhiteSpace(space.LinkedCheckoutsJson))
            {
                try
                {
                    linkedCheckouts = JsonSerializer.Deserialize<List<LinkedCheckoutDto>>(space.LinkedCheckoutsJson, jsonOptions) ?? new List<LinkedCheckoutDto>();
                }
                catch { }
            }

            return new AdminCommunitySpaceDto
            {
                Id = space.Id.ToString(),
                Name = space.Name,
                Telegram_link = space.TelegramLink,
                Zoom_link = space.ZoomLink,
                Product_ids = parsedIds.Select(id => id.ToString()).ToList(),
                Linked_checkouts = linkedCheckouts
            };
        }).ToList();
    }
}
