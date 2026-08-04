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

        if (session.Status == "COMPLETED")
        {
            return new CheckoutStatusDto("COMPLETED", Token: null);
        }

        return new CheckoutStatusDto("PENDING", Token: null);
    }
}
