using System;
using System.Data;
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

        const string sql = @"
            SELECT 
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'REVENUE_GROSS' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Gross_revenue"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'EXPENSE_GATEWAY_FEE' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Total_gateway_fees"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'LIABILITY_TAX_PAYABLE' THEN ABS(""BaseCurrencyAmount"") ELSE 0 END), 0) as ""Total_tax_liabilities"",
                COALESCE(SUM(CASE WHEN ""AccountType"" = 'ASSET_CASH' THEN ""BaseCurrencyAmount"" ELSE 0 END), 0) as ""Net_revenue"",
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
}
