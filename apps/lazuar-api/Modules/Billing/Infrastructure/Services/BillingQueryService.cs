using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Lazuar.ApiTypes;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Contracts;

namespace Modules.Billing.Infrastructure.Services;

public class BillingQueryService : IBillingQueryService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BillingQueryService([FromKeyedServices("BillingSqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<FinancialSummaryDto> GetFinancialSummaryAsync(Guid organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        // Isolates dashboard Net Revenue strictly to customer sales activity by 
        // subtracting gateway fees, taxes, and refunds from gross revenue, 
        // completely ignoring unrelated operational expenses like utility credit top-ups.
        const string sql = @"
            SELECT 
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'REVENUE_GROSS' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Gross_revenue"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'EXPENSE_GATEWAY_FEE' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Total_gateway_fees"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'LIABILITY_TAX_PAYABLE' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Total_tax_liabilities"",
                (
                    COALESCE(SUM(CASE WHEN ""AccountType"" = 'REVENUE_GROSS' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = 'CONTRA_REVENUE_REFUNDS' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = 'EXPENSE_DISCOUNT' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = 'EXPENSE_GATEWAY_FEE' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN ""AccountType"" = 'LIABILITY_TAX_PAYABLE' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0)
                ) as ""Net_revenue"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'LIABILITY_DEFERRED_REVENUE' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Deferred_revenue"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'REVENUE_RECOGNIZED' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Recognized_revenue"",
                'MYR' as ""Currency""
            FROM billing.""LedgerLines"" l
            JOIN billing.""LedgerEntries"" e ON l.""LedgerEntryId"" = e.""Id""
            WHERE e.""OrganizationId"" = @OrgId";

        var result = await connection.QuerySingleOrDefaultAsync<FinancialSummaryDto>(sql, new { OrgId = organizationId });
        
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
}
