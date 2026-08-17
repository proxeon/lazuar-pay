using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Lazuar.ApiTypes;
using Modules.Commerce.Application.Queries;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record RawCheckoutSession(string Status, Guid ClientProfileId, Guid ProductId);

    public async Task<CheckoutStatusDto?> GetCheckoutStatusAsync(Guid organizationId, Guid sessionId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        // Org-bound status only — never mint portal magic tokens on anonymous status poll.
        const string sessionSql = @"
            SELECT ""Status"", ""ClientProfileId"", ""ProductId""
            FROM commerce.""CheckoutSessions""
            WHERE ""Id"" = @SessionId AND ""OrganizationId"" = @OrgId
            LIMIT 1";

        var session = await connection.QuerySingleOrDefaultAsync<RawCheckoutSession>(
            sessionSql,
            new { SessionId = sessionId, OrgId = organizationId });
        if (session == null) return null;

        return MapPublicCheckoutStatus(session.Status);
    }

    public async Task<Guid?> FindSubscriptionIdForCheckoutSessionAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT s.""Id""
            FROM commerce.""CheckoutSessions"" c
            INNER JOIN commerce.""Subscriptions"" s
                ON s.""OrganizationId"" = c.""OrganizationId""
               AND s.""ClientProfileId"" = c.""ClientProfileId""
               AND (c.""ProductId"" IS NULL OR s.""ProductId"" = c.""ProductId"")
            WHERE c.""Id"" = @SessionId
              AND c.""OrganizationId"" = @OrgId
              AND c.""Status"" = 'COMPLETED'
            ORDER BY s.""CreatedAt"" DESC
            LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            sql,
            new { SessionId = sessionId, OrgId = organizationId });
    }

    /// <summary>
    /// Public poller contract: COMPLETED only when the row is COMPLETED; EXPIRED is honest;
    /// OPEN (and anything else) is PENDING. Token is minted at the public endpoint.
    /// </summary>
    internal static CheckoutStatusDto? MapPublicCheckoutStatus(string? status)
    {
        if (status is null)
        {
            return null;
        }

        return status switch
        {
            "COMPLETED" => new CheckoutStatusDto("COMPLETED", Token: null),
            "EXPIRED" => new CheckoutStatusDto("EXPIRED", Token: null),
            _ => new CheckoutStatusDto("PENDING", Token: null)
        };
    }
}
