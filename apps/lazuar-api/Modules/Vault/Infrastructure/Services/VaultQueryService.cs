using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;
using Microsoft.Extensions.DependencyInjection;
using Modules.Vault.Application.Queries;

namespace Modules.Vault.Infrastructure.Services;

public class VaultQueryService : IVaultQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private class RawVaultAssetDto
    {
        public Guid Id { get; set; }
        public string ProductIdsJson { get; set; } = "";
        public string Name { get; set; } = "";
        public string CloudflareR2Url { get; set; } = "";
        public string LinkedCheckoutsJson { get; set; } = "[]";
    }

    public VaultQueryService([FromKeyedServices("VaultSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<VaultAssetDto>> GetAssetsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            WITH AssetProducts AS (
                SELECT v.""Id"", jsonb_array_elements_text(v.""ProductIds"")::uuid AS ""ProductId""
                FROM vault.""VaultAssets"" v
                WHERE v.""OrganizationId"" = @OrgId
            ),
            LinkedData AS (
                SELECT 
                    ap.""Id"",
                    jsonb_agg(
                        jsonb_build_object(
                            'id', p.""Id"",
                            'name', p.""Name"",
                            'slug', p.""Slug""
                        )
                    ) as LinkedCheckouts
                FROM AssetProducts ap
                JOIN commerce.""Products"" p ON ap.""ProductId"" = p.""Id""
                GROUP BY ap.""Id""
            )
            SELECT 
                v.""Id"", 
                v.""ProductIds""::text as ProductIdsJson, 
                v.""Name"", 
                v.""CloudflareR2Url"",
                COALESCE(ld.LinkedCheckouts::text, '[]') as LinkedCheckoutsJson
            FROM vault.""VaultAssets"" v
            LEFT JOIN LinkedData ld ON v.""Id"" = ld.""Id""
            WHERE v.""OrganizationId"" = @OrgId
            ORDER BY v.""Name""";

        var rawAssets = await connection.QueryAsync<RawVaultAssetDto>(sql, new { OrgId = organizationId });

        return rawAssets.Select(MapToDto).ToList();
    }

    public async Task<VaultAssetDto?> GetAssetByIdAsync(Guid organizationId, Guid assetId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            WITH AssetProducts AS (
                SELECT v.""Id"", jsonb_array_elements_text(v.""ProductIds"")::uuid AS ""ProductId""
                FROM vault.""VaultAssets"" v
                WHERE v.""OrganizationId"" = @OrgId AND v.""Id"" = @AssetId
            ),
            LinkedData AS (
                SELECT 
                    ap.""Id"",
                    jsonb_agg(
                        jsonb_build_object(
                            'id', p.""Id"",
                            'name', p.""Name"",
                            'slug', p.""Slug""
                        )
                    ) as LinkedCheckouts
                FROM AssetProducts ap
                JOIN commerce.""Products"" p ON ap.""ProductId"" = p.""Id""
                GROUP BY ap.""Id""
            )
            SELECT 
                v.""Id"", 
                v.""ProductIds""::text as ProductIdsJson, 
                v.""Name"", 
                v.""CloudflareR2Url"",
                COALESCE(ld.LinkedCheckouts::text, '[]') as LinkedCheckoutsJson
            FROM vault.""VaultAssets"" v
            LEFT JOIN LinkedData ld ON v.""Id"" = ld.""Id""
            WHERE v.""OrganizationId"" = @OrgId AND v.""Id"" = @AssetId
            LIMIT 1";

        var rawAsset = await connection.QuerySingleOrDefaultAsync<RawVaultAssetDto>(sql, new { OrgId = organizationId, AssetId = assetId });

        return rawAsset != null ? MapToDto(rawAsset) : null;
    }

    private static VaultAssetDto MapToDto(RawVaultAssetDto raw)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        List<Guid> parsedIds = new();
        if (!string.IsNullOrWhiteSpace(raw.ProductIdsJson))
        {
            try
            {
                parsedIds = JsonSerializer.Deserialize<List<Guid>>(raw.ProductIdsJson, jsonOptions) ?? new List<Guid>();
            }
            catch { }
        }

        List<LinkedCheckoutDto> linkedCheckouts = new();
        if (!string.IsNullOrWhiteSpace(raw.LinkedCheckoutsJson))
        {
            try
            {
                linkedCheckouts = JsonSerializer.Deserialize<List<LinkedCheckoutDto>>(raw.LinkedCheckoutsJson, jsonOptions) ?? new List<LinkedCheckoutDto>();
            }
            catch { }
        }

        return new VaultAssetDto
        {
            Id = raw.Id.ToString(),
            Name = raw.Name,
            Cloudflare_r2_url = raw.CloudflareR2Url,
            Product_ids = parsedIds.Select(id => id.ToString()).ToList(),
            Linked_checkouts = linkedCheckouts
        };
    }
}
