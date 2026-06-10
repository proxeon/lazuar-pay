using Dapper;
using System.Data;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Community.Application.Queries;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService
{
    private record RawPaymentRecordDto(
        Guid Id, decimal Amount, string Currency, string PaymentMethod,
        string? ReferenceNumber, string? ReceiptUrl, string RecordedBy,
        DateTime PeriodStart, DateTime PeriodEnd, string Status, string? Notes, DateTime CreatedAt);

    public async Task<PaginatedResponse<PaymentRecordDto>> GetPaymentHistoryAsync(Guid organizationId, Guid subscriptionId, int page, int limit)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;

        const string sql = @"
            SELECT COUNT(*)::int
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""SubscriptionId"" = @SubId;

            SELECT
                pr.""Id"", pr.""Amount"", pr.""Currency"", pr.""PaymentMethod"",
                pr.""ExternalReference"" as ReferenceNumber, pr.""ReceiptUrl"",
                pr.""RecordedBy"", pr.""PeriodStart"", pr.""PeriodEnd"",
                pr.""Status"", pr.""Notes"", pr.""CreatedAt""
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND pr.""SubscriptionId"" = @SubId
            ORDER BY pr.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;";

        using var multi = await connection.QueryMultipleAsync(sql, new { OrgId = organizationId, SubId = subscriptionId, Limit = limit, Offset = offset });
        var totalCount = await multi.ReadFirstAsync<int>();
        var rawLogs = (await multi.ReadAsync<RawPaymentRecordDto>()).ToList();

        if (totalCount == 0) return new PaginatedResponse<PaymentRecordDto>(Enumerable.Empty<PaymentRecordDto>(), 0, page, limit);

        var dtos = rawLogs.Select(r => new PaymentRecordDto
        {
            Id = r.Id.ToString(),
            Amount = (double)r.Amount,
            Currency = r.Currency,
            Payment_method = r.PaymentMethod,
            Reference_number = r.ReferenceNumber,
            Receipt_url = r.ReceiptUrl,
            Recorded_by = r.RecordedBy,
            Period_start = new DateTimeOffset(r.PeriodStart),
            Period_end = new DateTimeOffset(r.PeriodEnd),
            Status = r.Status,
            Notes = r.Notes,
            Created_at = new DateTimeOffset(r.CreatedAt)
        });

        return new PaginatedResponse<PaymentRecordDto>(dtos, totalCount, page, limit);
    }

    public async Task<IEnumerable<GlobalTransactionDto>> GetGlobalTransactionsAsync(Guid organizationId, DateTime? fromDate, DateTime? toDate, string? status)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                pr.""Id"", 
                pr.""Amount"", 
                pr.""Currency"", 
                pr.""PaymentMethod"", 
                pr.""Status"", 
                pr.""CreatedAt"", 
                s.""ClientProfileId""
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            WHERE s.""OrganizationId"" = @OrgId
            AND (@FromDate IS NULL OR pr.""CreatedAt"" >= @FromDate)
            AND (@ToDate IS NULL OR pr.""CreatedAt"" <= @ToDate)
            AND (@Status IS NULL OR pr.""Status"" = @Status)
            ORDER BY pr.""CreatedAt"" DESC
            LIMIT 500";

        return await connection.QueryAsync<GlobalTransactionDto>(sql, new 
        { 
            OrgId = organizationId, 
            FromDate = fromDate, 
            ToDate = toDate, 
            Status = status 
        });
    }
}
