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
            SELECT ""Id"", ""ProductIds""::text as ProductIdsJson, ""Name"", ""CloudflareR2Url""
            FROM vault.""VaultAssets""
            WHERE ""OrganizationId"" = @OrgId
            ORDER BY ""Name""";

        var rawAssets = await connection.QueryAsync<RawVaultAssetDto>(sql, new { OrgId = organizationId });

        return rawAssets.Select(MapToDto).ToList();
    }

    public async Task<VaultAssetDto?> GetAssetByIdAsync(Guid organizationId, Guid assetId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""Id"", ""ProductIds""::text as ProductIdsJson, ""Name"", ""CloudflareR2Url""
            FROM vault.""VaultAssets""
            WHERE ""OrganizationId"" = @OrgId AND ""Id"" = @AssetId
            LIMIT 1";

        var rawAsset = await connection.QuerySingleOrDefaultAsync<RawVaultAssetDto>(sql, new { OrgId = organizationId, AssetId = assetId });

        return rawAsset != null ? MapToDto(rawAsset) : null;
    }

    private static VaultAssetDto MapToDto(RawVaultAssetDto raw)
    {
        List<Guid> parsedIds = new();
        if (!string.IsNullOrWhiteSpace(raw.ProductIdsJson))
        {
            try
            {
                parsedIds = JsonSerializer.Deserialize<List<Guid>>(raw.ProductIdsJson) ?? new List<Guid>();
            }
            catch { }
        }

        return new VaultAssetDto
        {
            Id = raw.Id.ToString(),
            Name = raw.Name,
            Cloudflare_r2_url = raw.CloudflareR2Url,
            Product_ids = parsedIds.Select(id => id.ToString()).ToList()
        };
    }
}
