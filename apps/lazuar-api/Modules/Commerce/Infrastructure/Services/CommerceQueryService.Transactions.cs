using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    private record RawGlobalTxDto(
        Guid Id, 
        decimal Amount, 
        decimal FeeAmount, 
        decimal NetAmount, 
        string Currency, 
        string Status, 
        DateTime CreatedAt, 
        string CustomerName, 
        string CustomerEmail, 
        string? ProductName, 
        string PaymentMethod, 
        string? ExternalReference,
        int TotalCount
    );

    public async Task<PaginatedResponse<TransactionLogDto>> GetTransactionsAsync(
        Guid organizationId, 
        int page, 
        int limit, 
        string? status, 
        string? paymentMethod, 
        string? searchTerm = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;
        var searchPattern = string.IsNullOrWhiteSpace(searchTerm) ? null : $"%{searchTerm}%";

        var sql = @"
            SELECT 
                t.""Id"", 
                t.""Amount"",
                t.""FeeAmount"",
                t.""NetAmount"",
                t.""Currency"",
                t.""Status"",
                t.""CreatedAt"",
                t.""CustomerName"",
                t.""CustomerEmail"",
                t.""ProductName"",
                'GATEWAY' as ""PaymentMethod"",
                t.""ExternalReference"",
                (COUNT(*) OVER())::int AS ""TotalCount""
            FROM commerce.""TransactionLogs"" t
            WHERE t.""OrganizationId"" = @OrgId
            AND (@Status IS NULL OR t.""Status"" = @Status)
            AND (@SearchTerm IS NULL OR t.""CustomerName"" ILIKE @SearchTerm OR t.""CustomerEmail"" ILIKE @SearchTerm)
            ORDER BY t.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;";

        var rawTx = (await connection.QueryAsync<RawGlobalTxDto>(sql, new { 
            OrgId = organizationId, 
            Limit = limit, 
            Offset = offset, 
            SearchTerm = searchPattern, 
            Status = status 
        })).ToList();

        int totalCount = rawTx.FirstOrDefault()?.TotalCount ?? 0;

        if (totalCount == 0) 
        {
            return new PaginatedResponse<TransactionLogDto>(Enumerable.Empty<TransactionLogDto>(), 0, page, limit);
        }

        var dtos = rawTx.Select(t => new TransactionLogDto
        {
            Id = t.Id.ToString(),
            Amount = (double)t.Amount,
            Fee_amount = (double)t.FeeAmount,
            Net_amount = (double)t.NetAmount,
            Currency = t.Currency,
            Status = t.Status,
            Created_at = new DateTimeOffset(t.CreatedAt),
            Customer_name = t.CustomerName,
            Customer_email = t.CustomerEmail,
            Product_name = t.ProductName,
            Recorded_by_name = "SYSTEM",
            External_reference = t.ExternalReference
        });

        return new PaginatedResponse<TransactionLogDto>(dtos, totalCount, page, limit);
    }
}
