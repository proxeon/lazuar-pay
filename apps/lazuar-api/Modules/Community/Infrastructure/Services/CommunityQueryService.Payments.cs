using Dapper;
using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Community.Infrastructure.Services;

public partial class CommunityQueryService
{
    private record RawPaymentRecordDto(
        Guid Id, decimal Amount, string Currency, string PaymentMethod,
        string? ReferenceNumber, string? ReceiptUrl, string RecordedBy,
        DateTime PeriodStart, DateTime PeriodEnd, string Status, string? Notes, DateTime CreatedAt);

    private record RawGlobalTxDto(
        Guid Id, decimal Amount, string Currency, string PaymentMethod, 
        string Status, DateTime CreatedAt, string? RecordedBy, 
        string? ExternalReference, Guid ClientProfileId);

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

    public async Task<PaginatedResponse<TransactionLogDto>> GetGlobalTransactionsAsync(Guid organizationId, int page, int limit, string? status, string? paymentMethod, DateTime? fromDate, DateTime? toDate)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;

        var whereBuilder = new StringBuilder(@"WHERE s.""OrganizationId"" = @OrgId");
        var parameters = new DynamicParameters();
        parameters.Add("OrgId", organizationId);
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        if (fromDate.HasValue)
        {
            whereBuilder.Append(@" AND pr.""CreatedAt"" >= @FromDate");
            parameters.Add("FromDate", fromDate.Value);
        }
        if (toDate.HasValue)
        {
            whereBuilder.Append(@" AND pr.""CreatedAt"" <= @ToDate");
            parameters.Add("ToDate", toDate.Value);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            whereBuilder.Append(@" AND pr.""Status"" = @Status");
            parameters.Add("Status", status);
        }
        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            whereBuilder.Append(@" AND pr.""PaymentMethod"" = @PaymentMethod");
            parameters.Add("PaymentMethod", paymentMethod);
        }

        var whereClause = whereBuilder.ToString();

        var sql = $@"
            SELECT COUNT(*)::int 
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            {whereClause};

            SELECT 
                pr.""Id"", 
                pr.""Amount"", 
                pr.""Currency"", 
                pr.""PaymentMethod"", 
                pr.""Status"", 
                pr.""CreatedAt"", 
                pr.""RecordedBy"",
                pr.""ExternalReference"",
                s.""ClientProfileId""
            FROM community.""PaymentRecords"" pr
            JOIN community.""Subscriptions"" s ON pr.""SubscriptionId"" = s.""Id""
            {whereClause}
            ORDER BY pr.""CreatedAt"" DESC
            LIMIT @Limit OFFSET @Offset;
        ";

        using var multi = await connection.QueryMultipleAsync(sql, parameters);

        var totalCount = await multi.ReadFirstAsync<int>();
        var rawTx = (await multi.ReadAsync<RawGlobalTxDto>()).ToList();

        if (totalCount == 0) return new PaginatedResponse<TransactionLogDto>(Enumerable.Empty<TransactionLogDto>(), 0, page, limit);

        var members = await _oneQueryService.GetWorkspaceMembersAsync(organizationId);
        var adminDict = members.ToDictionary(m => m.GlobalUserId.ToString(), m => m.Name);

        var profileIds = rawTx.Select(x => x.ClientProfileId).Distinct();
        var profiles = await _crmQueryService.GetClientProfilesAsync(profileIds);
        var profileDict = profiles.ToDictionary(p => Guid.Parse(p.Id));

        string GetActorName(string? recordedBy) 
        {
            if (string.IsNullOrWhiteSpace(recordedBy)) return "Unknown";
            if (recordedBy == "SYSTEM" || recordedBy == "SYSTEM_REACTIVATION") return "System Automation";
            if (adminDict.TryGetValue(recordedBy, out var adminName)) return adminName;
            
            if (recordedBy.StartsWith("OPS_AGENT_ON_BEHALF_OF_")) 
            {
                var realId = recordedBy.Replace("OPS_AGENT_ON_BEHALF_OF_", "");
                if (adminDict.TryGetValue(realId, out var agentAdmin)) return agentAdmin + " (AI Agent)";
                return "AI Agent";
            }
            return "Admin";
        }

        var dtos = rawTx.Select(t =>
        {
            profileDict.TryGetValue(t.ClientProfileId, out var profile);
            return new TransactionLogDto
            {
                Id = t.Id.ToString(),
                Amount = (double)t.Amount,
                Currency = t.Currency,
                Payment_method = t.PaymentMethod,
                Status = t.Status,
                Created_at = new DateTimeOffset(t.CreatedAt),
                Customer_name = profile?.Full_name ?? "Unknown",
                Customer_email = profile?.Email ?? "Unknown",
                Recorded_by_name = GetActorName(t.RecordedBy),
                External_reference = t.ExternalReference
            };
        });

        return new PaginatedResponse<TransactionLogDto>(dtos, totalCount, page, limit);
    }
}
