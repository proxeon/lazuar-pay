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
        Guid Id, decimal Amount, decimal FeeAmount, decimal NetAmount, string Currency, string Status, DateTime CreatedAt, 
        string CustomerName, string CustomerEmail, string ProductName, string PaymentMethod, string ExternalReference);

    public async Task<PaginatedResponse<TransactionLogDto>> GetTransactionsAsync(Guid organizationId, int page, int limit, string? status, string? paymentMethod, string? searchTerm = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;
        var searchPattern = string.IsNullOrWhiteSpace(searchTerm) ? null : $"%{searchTerm}%";

        var sql = @"
            WITH TransactionData AS (
                SELECT 
                    le.""Id"",
                    le.""ReferenceId"",
                    le.""Timestamp"" as CreatedAt,
                    le.""Description"",
                    ABS(SUM(CASE WHEN ll.""AccountType"" = 'ASSET_CASH' THEN ll.""Amount"" ELSE 0 END)) as Amount,
                    ABS(SUM(CASE WHEN ll.""AccountType"" = 'EXPENSE_GATEWAY_FEE' THEN ll.""Amount"" ELSE 0 END)) as FeeAmount,
                    SUM(CASE WHEN ll.""AccountType"" = 'ASSET_CASH' THEN ll.""Amount"" ELSE 0 END) as RawAssetCash,
                    MAX(ll.""Currency"") as Currency,
                    'GATEWAY' as PaymentMethod,
                    le.""ReferenceType""
                FROM billing.""LedgerEntries"" le
                JOIN billing.""LedgerLines"" ll ON le.""Id"" = ll.""LedgerEntryId""
                WHERE le.""OrganizationId"" = @OrgId 
                  AND le.""ReferenceType"" IN ('GATEWAY_PAYMENT', 'GATEWAY_REFUND')
                GROUP BY le.""Id"", le.""ReferenceId"", le.""Timestamp"", le.""Description"", le.""ReferenceType""
                HAVING SUM(CASE WHEN ll.""AccountType"" = 'ASSET_CASH' THEN ll.""Amount"" ELSE 0 END) != 0
            ),
            ResolvedCustomers AS (
                SELECT 
                    td.*,
                    CASE WHEN td.RawAssetCash > 0 THEN 'CONFIRMED' ELSE 'REFUNDED' END as Status,
                    COALESCE(cp.""FullName"", 'Unknown') as CustomerName,
                    COALESCE(cp.""Email"", 'Unknown') as CustomerEmail,
                    p.""Name"" as ProductName
                FROM TransactionData td
                LEFT JOIN commerce.""Subscriptions"" s ON s.""Id""::text = split_part(td.""Description"", 'subscription ', 2)
                LEFT JOIN commerce.""Orders"" o ON o.""Id""::text = split_part(td.""Description"", 'order ', 2)
                LEFT JOIN crm.""ClientProfiles"" cp ON cp.""Id"" = COALESCE(s.""ClientProfileId"", o.""ClientProfileId"")
                LEFT JOIN commerce.""Products"" p ON p.""Id"" = COALESCE(s.""ProductId"", o.""ProductId"")
            )
            SELECT COUNT(*)::int 
            FROM ResolvedCustomers rc
            WHERE (@Status IS NULL OR rc.Status = @Status)
            AND (@SearchTerm IS NULL OR rc.CustomerName ILIKE @SearchTerm OR rc.CustomerEmail ILIKE @SearchTerm);

            SELECT 
                rc.""Id"", 
                rc.Amount,
                rc.FeeAmount,
                (rc.Amount - rc.FeeAmount) as NetAmount,
                rc.Currency,
                rc.Status,
                rc.CreatedAt,
                rc.CustomerName,
                rc.CustomerEmail,
                rc.ProductName,
                rc.PaymentMethod,
                rc.""ReferenceId"" as ExternalReference
            FROM ResolvedCustomers rc
            WHERE (@Status IS NULL OR rc.Status = @Status)
            AND (@SearchTerm IS NULL OR rc.CustomerName ILIKE @SearchTerm OR rc.CustomerEmail ILIKE @SearchTerm)
            ORDER BY rc.CreatedAt DESC
            LIMIT @Limit OFFSET @Offset;";

        using var multi = await connection.QueryMultipleAsync(sql, new { OrgId = organizationId, Limit = limit, Offset = offset, SearchTerm = searchPattern, Status = status });

        var totalCount = await multi.ReadFirstAsync<int>();
        var rawTx = (await multi.ReadAsync<RawGlobalTxDto>()).ToList();

        if (totalCount == 0) return new PaginatedResponse<TransactionLogDto>(Enumerable.Empty<TransactionLogDto>(), 0, page, limit);

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
            Payment_method = t.PaymentMethod,
            Recorded_by_name = "SYSTEM",
            External_reference = t.ExternalReference
        });

        return new PaginatedResponse<TransactionLogDto>(dtos, totalCount, page, limit);
    }
}
