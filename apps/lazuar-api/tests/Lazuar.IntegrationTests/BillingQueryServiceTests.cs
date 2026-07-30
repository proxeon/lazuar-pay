using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.Billing.Infrastructure.Services;
using Npgsql;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.IntegrationTests;

[TestFixture]
public class BillingQueryServiceTests
{
    // Prefer LAZUAR_TEST_PG (CI service Postgres); fall back to local docker-compose defaults.
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("LAZUAR_TEST_PG")
        ?? "Host=localhost;Port=5432;Database=lazuar_mvp;Username=postgres;Password=postgres;";

    [Test]
    public async Task GetFinancialSummaryAsync_ShouldCalculateNetRevenueCorrectly_AndIgnoreOperationalExpenses()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sqlFactory = Substitute.For<ISqlConnectionFactory>();
        sqlFactory.CreateConnection().Returns(new NpgsqlConnection(_connectionString));

        await using var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            Assert.Ignore($"Postgres unavailable ({ex.GetType().Name}). Start docker-compose db or set LAZUAR_TEST_PG.");
            return;
        }

        // Ensure minimal billing schema for CI (no full EF migrate required)
        await using (var setup = new NpgsqlCommand(@"
            CREATE SCHEMA IF NOT EXISTS billing;
            CREATE TABLE IF NOT EXISTS billing.""LedgerEntries"" (
                ""Id"" uuid PRIMARY KEY,
                ""OrganizationId"" uuid NOT NULL,
                ""Timestamp"" timestamptz NOT NULL,
                ""ReferenceType"" text NOT NULL,
                ""ReferenceId"" text NOT NULL,
                ""CustomerType"" text NOT NULL
            );
            CREATE TABLE IF NOT EXISTS billing.""LedgerLines"" (
                ""Id"" uuid PRIMARY KEY,
                ""LedgerEntryId"" uuid NOT NULL,
                ""AccountType"" text NOT NULL,
                ""Amount"" numeric NOT NULL,
                ""Currency"" text NOT NULL,
                ""BaseCurrencyAmount"" numeric NOT NULL,
                ""BaseCurrency"" text NOT NULL,
                ""TaxTypeCode"" text NULL,
                ""MsicCode"" text NULL
            );
        ", connection))
        {
            await setup.ExecuteNonQueryAsync();
        }

        try
        {
            // Seed Data:
            // 1. A Customer Sale (Gross 100, Gateway Fee 5, Tax 10)
            // 2. A Creator Top-Up (Software Expense 50, Cash Output -50)
            var entryId1 = Guid.CreateVersion7();
            var entryId2 = Guid.CreateVersion7();

            await using (var cmd = new NpgsqlCommand(@"
                INSERT INTO billing.""LedgerEntries"" (""Id"", ""OrganizationId"", ""Timestamp"", ""ReferenceType"", ""ReferenceId"", ""CustomerType"")
                VALUES
                (@Id1, @OrgId, NOW(), 'SALE', 'test_sale_ref', 'B2C'),
                (@Id2, @OrgId, NOW(), 'TOPUP', 'test_topup_ref', 'B2B');

                INSERT INTO billing.""LedgerLines"" (""Id"", ""LedgerEntryId"", ""AccountType"", ""Amount"", ""Currency"", ""BaseCurrencyAmount"", ""BaseCurrency"", ""TaxTypeCode"", ""MsicCode"")
                VALUES
                (gen_random_uuid(), @Id1, 'REVENUE_GROSS', -100, 'MYR', -100, 'MYR', '06', '004'),
                (gen_random_uuid(), @Id1, 'EXPENSE_GATEWAY_FEE', 5, 'MYR', 5, 'MYR', '06', '004'),
                (gen_random_uuid(), @Id1, 'LIABILITY_TAX_PAYABLE', -10, 'MYR', -10, 'MYR', '06', '004'),
                (gen_random_uuid(), @Id2, 'EXPENSE_SOFTWARE_SUBSCRIPTION', 50, 'MYR', 50, 'MYR', '06', '004'),
                (gen_random_uuid(), @Id2, 'ASSET_CASH', -50, 'MYR', -50, 'MYR', '06', '004');
            ", connection))
            {
                cmd.Parameters.AddWithValue("Id1", entryId1);
                cmd.Parameters.AddWithValue("Id2", entryId2);
                cmd.Parameters.AddWithValue("OrgId", orgId);
                await cmd.ExecuteNonQueryAsync();
            }

            var service = new BillingQueryService(sqlFactory);

            // Act
            var summary = await service.GetFinancialSummaryAsync(orgId);

            // Assert
            summary.Gross_revenue.Should().Be(100);
            summary.Total_gateway_fees.Should().Be(5);
            summary.Total_tax_liabilities.Should().Be(10);

            // Expected Net Revenue = Gross (100) - Fee (5) - Tax (10) = 85.
            // The 50 MYR operational expense (Top-Up) MUST be completely ignored.
            summary.Net_revenue.Should().Be(85);
        }
        finally
        {
            await using var cmd = new NpgsqlCommand(@"DELETE FROM billing.""LedgerEntries"" WHERE ""OrganizationId"" = @OrgId;", connection);
            cmd.Parameters.AddWithValue("OrgId", orgId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
