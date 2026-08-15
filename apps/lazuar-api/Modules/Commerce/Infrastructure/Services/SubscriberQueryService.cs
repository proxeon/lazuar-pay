using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.CRM.Contracts;
using Modules.Commerce.Contracts;

namespace Modules.Commerce.Infrastructure.Services;

public class SubscriberQueryService : ISubscriberQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICrmQueryService _crmQueryService;
    private readonly CommerceDbContext _dbContext;

    public SubscriberQueryService(
        [FromKeyedServices("CommerceSqlConnectionFactory")] ISqlConnectionFactory connectionFactory,
        ICrmQueryService crmQueryService,
        CommerceDbContext dbContext)
    {
        _connectionFactory = connectionFactory;
        _crmQueryService = crmQueryService;
        _dbContext = dbContext;
    }

    public async Task<int> GetActiveSubscriberCountAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT COUNT(*) 
            FROM commerce.""Subscriptions"" s
            WHERE s.""OrganizationId"" = @OrgId 
            AND s.""Status"" IN ('ACTIVE', 'PAST_DUE');";

        return await connection.ExecuteScalarAsync<int>(sql, new { OrgId = organizationId });
    }

    public async Task<IReadOnlyList<SubscriberRecipient>> GetActiveSubscriberRecipientsAsync(Guid organizationId, int page, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        var offset = (page - 1) * limit;
        const string sql = @"
            SELECT s.""Id"", s.""ClientProfileId""
            FROM commerce.""Subscriptions"" s
            WHERE s.""OrganizationId"" = @OrgId 
            AND s.""Status"" IN ('ACTIVE', 'PAST_DUE')
            ORDER BY s.""CreatedAt""
            LIMIT @Limit OFFSET @Offset;";

        var rows = (await connection.QueryAsync<(Guid Id, Guid ClientProfileId)>(
            sql, new { OrgId = organizationId, Limit = limit, Offset = offset })).ToList();

        if (!rows.Any()) return Array.Empty<SubscriberRecipient>();

        var profileIds = rows.Select(r => r.ClientProfileId).Distinct().ToList();
        var profiles = (await _crmQueryService.GetClientProfilesAsync(profileIds)).ToDictionary(p => Guid.Parse(p.Id));

        var result = new List<SubscriberRecipient>(rows.Count);
        foreach (var row in rows)
        {
            // Marketing broadcasts require explicit marketing consent (PDPA).
            if (profiles.TryGetValue(row.ClientProfileId, out var profile)
                && !string.IsNullOrWhiteSpace(profile.Email)
                && profile.Consented_to_marketing)
            {
                result.Add(new SubscriberRecipient(row.Id, profile.Email, profile.Phone, profile.Full_name));
            }
        }
        return result;
    }

    public async Task<SubscriptionMailContext?> GetSubscriptionMailContextAsync(Guid organizationId, Guid subscriptionId)
    {
        var sub = await _dbContext.Subscriptions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId);

        if (sub == null) return null;

        var product = await _dbContext.Products
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == sub.ProductId);

        return new SubscriptionMailContext(
            sub.Id,
            sub.ProductId,
            product?.Name ?? "",
            product?.Price ?? 0m,
            product?.Currency ?? "",
            sub.NextBillingDate,
            sub.Status);
    }
}
