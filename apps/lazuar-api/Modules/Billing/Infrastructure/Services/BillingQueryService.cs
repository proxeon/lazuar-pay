using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts;
using Modules.Billing.Domain;

namespace Modules.Billing.Infrastructure.Services;

public class BillingQueryService : IBillingQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private record RawLedgerEntryDto(
        Guid Id, DateTime Timestamp, string ReferenceType, string ReferenceId,
        string? Description, string CustomerType, string? TaxInvoiceId,
        string? CustomerDocumentNumber, string? LhdnValidationStatus, int TotalCount);

    private record RawLedgerLineDto(
        Guid Id, Guid LedgerEntryId, string AccountType, decimal Amount,
        string Currency, decimal BaseCurrencyAmount, string BaseCurrency);

    public BillingQueryService([FromKeyedServices("BillingSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PaginatedResponse<LedgerEntryDto>> GetLedgerEntriesAsync(Guid organizationId, int page, int limit, string? search, string? typeFilter, DateTime? fromDate, DateTime? toDate)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        int offset = (page - 1) * limit;
        var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

        // Dynamically build the query to avoid Npgsql parameter type inference errors on NULLs
        var sqlBuilder = new StringBuilder(@"
            SELECT 
                e.""Id"", e.""Timestamp"", e.""ReferenceType"", e.""ReferenceId"", 
                e.""Description"", e.""CustomerType"", e.""TaxInvoiceId"", e.""CustomerDocumentNumber"", e.""LhdnValidationStatus"",
                (COUNT(*) OVER())::int AS ""TotalCount""
            FROM billing.""LedgerEntries"" e
            WHERE e.""OrganizationId"" = @OrgId");

        if (!string.IsNullOrWhiteSpace(searchPattern))
        {
            sqlBuilder.Append(@" AND (e.""ReferenceId"" ILIKE @Search OR e.""TaxInvoiceId"" ILIKE @Search OR e.""CustomerDocumentNumber"" ILIKE @Search)");
        }

        if (typeFilter == "sales")
        {
            sqlBuilder.Append($@" AND e.""ReferenceType"" NOT IN ('{LedgerReferenceTypes.GatewayRefund}', '{LedgerReferenceTypes.LhdnCancellation}')");
        }
        else if (typeFilter == "reversals")
        {
            sqlBuilder.Append($@" AND e.""ReferenceType"" IN ('{LedgerReferenceTypes.GatewayRefund}', '{LedgerReferenceTypes.LhdnCancellation}')");
        }

        if (fromDate.HasValue)
        {
            sqlBuilder.Append(@" AND e.""Timestamp"" >= @FromDate");
        }

        if (toDate.HasValue)
        {
            sqlBuilder.Append(@" AND e.""Timestamp"" <= @ToDate");
        }

        sqlBuilder.Append(@" ORDER BY e.""Timestamp"" DESC LIMIT @Limit OFFSET @Offset;");

        var entries = (await connection.QueryAsync<RawLedgerEntryDto>(sqlBuilder.ToString(), new {
            OrgId = organizationId,
            Limit = limit,
            Offset = offset,
            Search = searchPattern,
            FromDate = fromDate,
            ToDate = toDate
        })).ToList();

        if (!entries.Any())
            return new PaginatedResponse<LedgerEntryDto>(Enumerable.Empty<LedgerEntryDto>(), 0, page, limit);

        int totalCount = entries.First().TotalCount;
        var entryIds = entries.Select(e => e.Id).ToList();

        var linesSql = @"
            SELECT ""Id"", ""LedgerEntryId"", ""AccountType"", ""Amount"", ""Currency"", ""BaseCurrencyAmount"", ""BaseCurrency""
            FROM billing.""LedgerLines""
            WHERE ""LedgerEntryId"" = ANY(@EntryIds)";

        var lines = (await connection.QueryAsync<RawLedgerLineDto>(linesSql, new { EntryIds = entryIds })).ToList();
        var linesLookup = lines.GroupBy(l => l.LedgerEntryId).ToDictionary(g => g.Key, g => g.ToList());

        var dtos = entries.Select(e => new LedgerEntryDto
        {
            Id = e.Id.ToString(),
            Timestamp = new DateTimeOffset(e.Timestamp),
            Reference_type = e.ReferenceType,
            Reference_id = e.ReferenceId,
            Description = e.Description,
            Customer_type = e.CustomerType,
            Tax_invoice_id = e.TaxInvoiceId,
            Customer_document_number = e.CustomerDocumentNumber,
            Lhdn_validation_status = e.LhdnValidationStatus,
            Lines = linesLookup.ContainsKey(e.Id) ? linesLookup[e.Id].Select(l => new LedgerLineDto
            {
                Id = l.Id.ToString(),
                Ledger_entry_id = l.LedgerEntryId.ToString(),
                Account_type = l.AccountType,
                Amount = (double)l.Amount,
                Currency = l.Currency,
                Base_currency_amount = (double)l.BaseCurrencyAmount,
                Base_currency = l.BaseCurrency
            }).ToList() : new List<LedgerLineDto>()
        });

        return new PaginatedResponse<LedgerEntryDto>(dtos, totalCount, page, limit);
    }

    public async Task<FinancialSummaryDto> GetFinancialSummaryAsync(Guid organizationId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        // Signed double-entry (credits on revenue/liability are negative).
        // Use signed sums so refunds/cancellations/recognition net correctly;
        // ABS inflated gross/fees/tax and deferred under reversals.
        // Display polarity:
        //   revenue/tax/deferred/recognized (credit-normal) → -SUM
        //   fees/discounts/contra-refunds (debit-normal)    → +SUM
        var sqlBuilder = new StringBuilder($@"
            SELECT 
                COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.RevenueGross}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Gross_revenue"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = '{AccountTypes.ExpenseGatewayFee}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Total_gateway_fees"",
                COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.LiabilityTaxPayable}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Total_tax_liabilities"",
                (
                    COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.RevenueGross}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = '{AccountTypes.ContraRevenueRefunds}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = '{AccountTypes.ExpenseDiscount}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = '{AccountTypes.ExpenseGatewayFee}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.LiabilityTaxPayable}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0)
                ) as ""Net_revenue"",
                COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.LiabilityDeferredRevenue}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Deferred_revenue"",
                COALESCE(-SUM(CASE WHEN ""AccountType"" = '{AccountTypes.RevenueRecognized}' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Recognized_revenue"",
                'MYR' as ""Currency""
            FROM billing.""LedgerLines"" l
            JOIN billing.""LedgerEntries"" e ON l.""LedgerEntryId"" = e.""Id""
            WHERE e.""OrganizationId"" = @OrgId");

        if (fromDate.HasValue)
            sqlBuilder.Append(@" AND e.""Timestamp"" >= @FromDate");
        if (toDate.HasValue)
            sqlBuilder.Append(@" AND e.""Timestamp"" <= @ToDate");

        var result = await connection.QuerySingleOrDefaultAsync<FinancialSummaryDto>(sqlBuilder.ToString(), new
        {
            OrgId = organizationId,
            FromDate = fromDate,
            ToDate = toDate
        });

        return result ?? new FinancialSummaryDto
        {
            Gross_revenue = 0,
            Total_gateway_fees = 0,
            Total_tax_liabilities = 0,
            Net_revenue = 0,
            Deferred_revenue = 0,
            Recognized_revenue = 0,
            Currency = "MYR"
        };
    }

    public async Task<IReadOnlyList<NetProfitDto>> GetNetProfitAsync(Guid organizationId, string period = "monthly")
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        var periodExpr = string.Equals(period, "yearly", StringComparison.OrdinalIgnoreCase)
            ? @"to_char(e.""Timestamp"" AT TIME ZONE 'UTC', 'YYYY')"
            : @"to_char(e.""Timestamp"" AT TIME ZONE 'UTC', 'YYYY-MM')";

        var sql = $@"
            SELECT
                {periodExpr} as ""Period"",
                COALESCE(-SUM(CASE WHEN l.""AccountType"" = '{AccountTypes.RevenueGross}' THEN l.""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Gross_revenue"",
                COALESCE(SUM(CASE WHEN l.""AccountType"" = '{AccountTypes.ExpenseGatewayFee}' THEN l.""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Gateway_fees"",
                COALESCE(SUM(CASE WHEN l.""AccountType"" = '{AccountTypes.ContraRevenueRefunds}' THEN l.""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Refunds_issued"",
                (
                    COALESCE(-SUM(CASE WHEN l.""AccountType"" = '{AccountTypes.RevenueGross}' THEN l.""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN l.""AccountType"" = '{AccountTypes.ContraRevenueRefunds}' THEN l.""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN l.""AccountType"" = '{AccountTypes.ExpenseGatewayFee}' THEN l.""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN l.""AccountType"" = '{AccountTypes.ExpenseDiscount}' THEN l.""BaseCurrencyAmount"" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN l.""AccountType"" = '{AccountTypes.ExpenseCommission}' THEN l.""BaseCurrencyAmount"" ELSE 0 END), 0)
                ) as ""Net_profit"",
                'MYR' as ""Currency""
            FROM billing.""LedgerLines"" l
            JOIN billing.""LedgerEntries"" e ON l.""LedgerEntryId"" = e.""Id""
            WHERE e.""OrganizationId"" = @OrgId
            GROUP BY 1
            ORDER BY 1 DESC";

        var rows = await connection.QueryAsync<NetProfitDto>(sql, new { OrgId = organizationId });
        return rows.ToList();
    }

    public async Task<bool> HasPositiveCreditBalanceAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""AvailableCredits"" 
            FROM billing.""TenantCreditBalances"" 
            WHERE ""OrganizationId"" = @OrgId 
            LIMIT 1";

        var credits = await connection.QuerySingleOrDefaultAsync<int?>(sql, new { OrgId = organizationId });

        return credits.HasValue && credits.Value > 0;
    }

    public async Task<int> GetAvailableCreditsAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""AvailableCredits""
            FROM billing.""TenantCreditBalances""
            WHERE ""OrganizationId"" = @OrgId
            LIMIT 1";

        var credits = await connection.QuerySingleOrDefaultAsync<int?>(sql, new { OrgId = organizationId });
        return credits ?? 0;
    }

    public async Task<bool> HasSufficientCreditsAsync(Guid organizationId, int amount)
    {
        if (amount <= 0) return true;
        var available = await GetAvailableCreditsAsync(organizationId);
        return available >= amount;
    }

    public async Task<CreditBalanceDto> GetCreditBalanceWithHistoryAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string balanceSql = @"
            SELECT ""Id"", ""AvailableCredits"" 
            FROM billing.""TenantCreditBalances"" 
            WHERE ""OrganizationId"" = @OrgId 
            LIMIT 1";

        var balanceRow = await connection.QuerySingleOrDefaultAsync<dynamic>(balanceSql, new { OrgId = organizationId });

        if (balanceRow == null)
        {
            return new CreditBalanceDto
            {
                Available_credits = 0,
                Recent_transactions = new List<CreditTransactionDto>()
            };
        }

        const string historySql = @"
            SELECT ""Amount"", ""Reference"", ""CreatedAt"" as Created_at
            FROM billing.""CreditLedgers""
            WHERE ""TenantCreditBalanceId"" = @BalanceId
            ORDER BY ""CreatedAt"" DESC
            LIMIT 50";

        var history = await connection.QueryAsync<CreditTransactionDto>(historySql, new { BalanceId = (Guid)balanceRow.Id });

        return new CreditBalanceDto
        {
            Available_credits = (int)balanceRow.AvailableCredits,
            Recent_transactions = history.ToList()
        };
    }

    public async Task<TenantBillingProfileDto?> GetBillingProfileAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT ""LegalName"", ""Tin"", ""RegistrationNumber"", ""SstRegistrationNumber"", ""LogoUrl"",
                   ""AddressLine1"" as Line1, ""AddressLine2"" as Line2, ""AddressLine3"" as Line3, 
                   ""City"", ""PostalCode"", ""StateCode"", ""CountryCode""
            FROM billing.""TenantBillingProfiles""
            WHERE ""OrganizationId"" = @OrgId 
            LIMIT 1";

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { OrgId = organizationId });

        if (row == null) return null;

        TenantBillingAddressDto? address = null;
        if (!string.IsNullOrWhiteSpace((string?)row.Line1) && !string.IsNullOrWhiteSpace((string?)row.City))
        {
            address = new TenantBillingAddressDto
            {
                Line1 = row.Line1,
                Line2 = row.Line2,
                Line3 = row.Line3,
                City = row.City,
                Postal_code = row.PostalCode,
                State_code = row.StateCode,
                Country_code = row.CountryCode
            };
        }

        return new TenantBillingProfileDto
        {
            Legal_name = row.LegalName,
            Tin = row.Tin,
            Registration_number = row.RegistrationNumber,
            Sst_registration_number = row.SstRegistrationNumber,
            Logo_url = row.LogoUrl,
            Address = address
        };
    }

    public Task<LedgerDocumentIdentity?> FindPaymentByGatewayTransactionAsync(
        Guid organizationId,
        string gatewayTransactionId)
    {
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
            return Task.FromResult<LedgerDocumentIdentity?>(null);

        return FindLedgerByReferenceAsync(organizationId, LedgerReferenceTypes.GatewayPayment, gatewayTransactionId);
    }

    public async Task<LedgerDocumentIdentity?> FindLedgerByReferenceAsync(
        Guid organizationId,
        string referenceType,
        string referenceId)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
            return null;

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT e.""Id"", e.""ReferenceType"", e.""ReferenceId"", e.""CustomerDocumentNumber"",
                   e.""LhdnDocumentUuid"", e.""TaxInvoiceId"", e.""CustomerType"", e.""LhdnValidationStatus"",
                   e.""Timestamp""
            FROM billing.""LedgerEntries"" e
            WHERE e.""OrganizationId"" = @OrgId
              AND e.""ReferenceType"" = @ReferenceType
              AND e.""ReferenceId"" = @ReferenceId
            LIMIT 1";

        var row = await connection.QuerySingleOrDefaultAsync<RawIdentityRow>(sql, new
        {
            OrgId = organizationId,
            ReferenceType = referenceType,
            ReferenceId = referenceId
        });

        if (row == null) return null;

        var (amount, currency) = await SumEntryAsync(connection, row.Id);
        return MapIdentity(row, amount, currency);
    }

    public async Task<IReadOnlyList<LedgerDocumentIdentity>> GetDocumentsByReferenceIdsAsync(
        Guid organizationId,
        IReadOnlyList<string> referenceIds)
    {
        var ids = referenceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
            return Array.Empty<LedgerDocumentIdentity>();

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT e.""Id"", e.""ReferenceType"", e.""ReferenceId"", e.""CustomerDocumentNumber"",
                   e.""LhdnDocumentUuid"", e.""TaxInvoiceId"", e.""CustomerType"", e.""LhdnValidationStatus"",
                   e.""Timestamp""
            FROM billing.""LedgerEntries"" e
            WHERE e.""OrganizationId"" = @OrgId
              AND e.""ReferenceId"" = ANY(@Refs)
              AND e.""CustomerDocumentNumber"" IS NOT NULL
              AND e.""ReferenceType"" IN ('GATEWAY_PAYMENT', 'GATEWAY_REFUND', 'MANUAL_ENROLLMENT', 'LHDN_CANCELLATION')
            ORDER BY e.""Timestamp"" DESC";

        var rows = (await connection.QueryAsync<RawIdentityRow>(sql, new { OrgId = organizationId, Refs = ids.ToArray() })).ToList();
        if (rows.Count == 0)
            return Array.Empty<LedgerDocumentIdentity>();

        var result = new List<LedgerDocumentIdentity>(rows.Count);
        foreach (var row in rows)
        {
            var (amount, currency) = await SumEntryAsync(connection, row.Id);
            result.Add(MapIdentity(row, amount, currency));
        }

        return result;
    }

    private static LedgerDocumentIdentity MapIdentity(RawIdentityRow row, decimal amount, string currency) =>
        new(
            row.Id,
            row.ReferenceType,
            row.ReferenceId,
            row.CustomerDocumentNumber,
            row.LhdnDocumentUuid,
            row.TaxInvoiceId,
            row.CustomerType,
            row.LhdnValidationStatus,
            amount,
            currency,
            row.Timestamp);

    private static async Task<(decimal Amount, string Currency)> SumEntryAsync(IDbConnection connection, Guid entryId)
    {
        const string sql = @"
            SELECT COALESCE(SUM(ABS(""Amount"")), 0) as Amount,
                   COALESCE(MAX(""Currency""), 'MYR') as Currency
            FROM billing.""LedgerLines""
            WHERE ""LedgerEntryId"" = @Id
              AND ""AccountType"" IN ('REVENUE_GROSS', 'REVENUE_RECOGNIZED', 'CONTRA_REVENUE_REFUNDS', 'LIABILITY_TAX_PAYABLE')";

        var row = await connection.QuerySingleAsync<(decimal Amount, string Currency)>(sql, new { Id = entryId });
        return row;
    }

    private record RawIdentityRow(
        Guid Id,
        string ReferenceType,
        string ReferenceId,
        string? CustomerDocumentNumber,
        string? LhdnDocumentUuid,
        string? TaxInvoiceId,
        string CustomerType,
        string? LhdnValidationStatus,
        DateTime Timestamp);
}
