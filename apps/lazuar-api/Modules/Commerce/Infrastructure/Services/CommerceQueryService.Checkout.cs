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

    public async Task<CheckoutStatusDto?> GetCheckoutStatusAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sessionSql = @"
            SELECT ""Status"", ""ClientProfileId"", ""ProductId""
            FROM commerce.""CheckoutSessions""
            WHERE ""Id"" = @SessionId
            LIMIT 1";

        var session = await connection.QuerySingleOrDefaultAsync<RawCheckoutSession>(sessionSql, new { SessionId = sessionId });
        if (session == null) return null;

        if (session.Status == "COMPLETED")
        {
            const string subSql = @"
                SELECT ""Id""
                FROM commerce.""Subscriptions""
                WHERE ""ClientProfileId"" = @ProfileId 
                  AND ""ProductId"" = @ProductId 
                  AND ""Status"" = 'ACTIVE'
                LIMIT 1";

            var subId = await connection.QuerySingleOrDefaultAsync<Guid?>(subSql, new { ProfileId = session.ClientProfileId, ProductId = session.ProductId });
            
            string? token = null;
            if (subId.HasValue)
            {
                token = _tokenService.GenerateToken(subId.Value);
            }

            return new CheckoutStatusDto("COMPLETED", token);
        }

        return new CheckoutStatusDto("PENDING", null);
    }
}
