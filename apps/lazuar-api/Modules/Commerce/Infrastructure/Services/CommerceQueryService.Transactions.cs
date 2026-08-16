using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;
using Modules.Commerce.Application;
using Modules.Payments.Contracts;

namespace Modules.Commerce.Infrastructure.Services;

public partial class CommerceQueryService
{
    internal record RawGlobalTxDto(
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
        string RecordedByName,
        string? ExternalReference,
        string? GatewayName,
        decimal RefundedAmount,
        int TotalCount
    );

    public async Task<PaginatedResponse<TransactionLogDto>> GetTransactionsAsync(
        Guid organizationId,
        int page,
        int limit,
        string? status,
        string? gatewayName,
        string? searchTerm = null,
        Guid? subscriptionId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;
        var searchPattern = subscriptionId.HasValue || string.IsNullOrWhiteSpace(searchTerm)
            ? null
            : $"%{searchTerm}%";
        var gatewayFilter = string.IsNullOrWhiteSpace(gatewayName) ? null : gatewayName.Trim().ToUpperInvariant();

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
                COALESCE(NULLIF(t.""RecordedByName"", ''), 'GATEWAY') as ""RecordedByName"",
                t.""ExternalReference"",
                t.""GatewayName"",
                t.""RefundedAmount"",
                (COUNT(*) OVER())::int AS ""TotalCount""
            FROM commerce.""TransactionLogs"" t
            WHERE t.""OrganizationId"" = @OrgId
            AND (@Status IS NULL OR t.""Status"" = @Status)
            AND (@GatewayName IS NULL OR t.""GatewayName"" = @GatewayName)
            AND (@SubscriptionId IS NULL OR t.""SubscriptionId"" = @SubscriptionId)
            AND (@SearchTerm IS NULL OR t.""CustomerName"" ILIKE @SearchTerm OR t.""CustomerEmail"" ILIKE @SearchTerm)
            ORDER BY t.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;";

        var rawTx = (await connection.QueryAsync<RawGlobalTxDto>(sql, new {
            OrgId = organizationId,
            Limit = limit,
            Offset = offset,
            SearchTerm = searchPattern,
            Status = status,
            GatewayName = gatewayFilter,
            SubscriptionId = subscriptionId
        })).ToList();

        int totalCount = rawTx.FirstOrDefault()?.TotalCount ?? 0;

        if (totalCount == 0)
        {
            return new PaginatedResponse<TransactionLogDto>(Enumerable.Empty<TransactionLogDto>(), 0, page, limit);
        }

        return new PaginatedResponse<TransactionLogDto>(rawTx.Select(MapTransactionLog), totalCount, page, limit);
    }

    public async Task<(IReadOnlyList<TransactionExportCsv.Row> Rows, bool Truncated)> ExportTransactionsAsync(
        Guid organizationId,
        DateTime fromUtc,
        DateTime toUtc,
        string? status)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT
                t.""Id"",
                t.""CreatedAt"",
                t.""Status"",
                t.""Amount"",
                t.""FeeAmount"",
                t.""NetAmount"",
                t.""Currency"",
                t.""CustomerName"",
                t.""CustomerEmail"",
                t.""ProductName"",
                COALESCE(NULLIF(t.""RecordedByName"", ''), 'GATEWAY') as ""RecordedBy"",
                t.""ExternalReference""
            FROM commerce.""TransactionLogs"" t
            WHERE t.""OrganizationId"" = @OrgId
            AND t.""CreatedAt"" >= @FromUtc
            AND t.""CreatedAt"" <= @ToUtc
            AND (@Status IS NULL OR t.""Status"" = @Status)
            ORDER BY t.""CreatedAt"" DESC
            LIMIT @Limit;";

        var rows = (await connection.QueryAsync<TransactionExportCsv.Row>(sql, new
        {
            OrgId = organizationId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
            Limit = TransactionExportCsv.HardCap + 1
        })).ToList();

        var truncated = rows.Count > TransactionExportCsv.HardCap;
        if (truncated)
        {
            rows = rows.Take(TransactionExportCsv.HardCap).ToList();
        }

        return (rows, truncated);
    }

    internal static TransactionLogDto MapTransactionLog(RawGlobalTxDto t)
    {
        var refunded = t.RefundedAmount < 0 ? 0m : t.RefundedAmount;
        var remaining = t.Amount - refunded;
        if (remaining < 0) remaining = 0m;

        return new TransactionLogDto
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
            Recorded_by_name = t.RecordedByName,
            External_reference = t.ExternalReference,
            Gateway_name = t.GatewayName,
            Refunded_amount = (double)refunded,
            Remaining_amount = (double)remaining,
            Supports_api_refund = PaymentGatewayCapabilities.SupportsApiRefund(t.GatewayName)
        };
    }
}
