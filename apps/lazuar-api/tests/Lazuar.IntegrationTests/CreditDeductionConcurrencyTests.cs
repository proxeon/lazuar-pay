using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Commands;
using NSubstitute;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace Lazuar.IntegrationTests;

/// <summary>
/// C.9 — Real Postgres concurrent credit deduct + idempotency.
/// Uses Testcontainers; skips when Docker is unavailable.
/// Residual: xmin concurrency is not exercised under EF InMemory (see ModuleTests sequential suite).
/// </summary>
[TestFixture]
public class CreditDeductionConcurrencyTests
{
    private PostgreSqlContainer? _dbContainer;
    private string _connectionString = null!;
    private bool _postgresReady;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        try
        {
#pragma warning disable CS0618
            _dbContainer = new PostgreSqlBuilder()
                .WithDatabase("lazuar_credit_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
#pragma warning restore CS0618

            await _dbContainer.StartAsync();
            _connectionString = _dbContainer.GetConnectionString();

            await using var migrateCtx = CreateDbContext(Guid.Empty);
            await migrateCtx.Database.MigrateAsync();
            _postgresReady = true;
        }
        catch (Exception ex)
        {
            _postgresReady = false;
            TestContext.WriteLine($"Postgres Testcontainers unavailable: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_dbContainer is not null)
            await _dbContainer.DisposeAsync();
    }

    private BillingDbContext CreateDbContext(Guid ambientTenant)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(_connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "billing");
            })
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(ambientTenant);

        return new BillingDbContext(
            options,
            executionContext,
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());
    }

    private void RequirePostgres()
    {
        if (!_postgresReady)
            Assert.Ignore("Postgres Testcontainers unavailable (Docker required for concurrent credit tests).");
    }

    private async Task<Guid> SeedWalletAsync(int credits)
    {
        var orgId = Guid.CreateVersion7();
        await using var db = CreateDbContext(Guid.Empty);
        var wallet = new TenantCreditBalance(orgId);
        wallet.TopUp(credits, "seed");
        db.TenantCreditBalances.Add(wallet);
        await db.SaveChangesAsync();
        return orgId;
    }

    [Test]
    public async Task Concurrent_SameIdempotencyKey_DeductsOnce()
    {
        RequirePostgres();
        var orgId = await SeedWalletAsync(100);
        const string key = "concurrent-same-key";
        const int amount = 25;

        async Task RunOnce()
        {
            await using var db = CreateDbContext(Guid.Empty);
            var handler = new DeductTenantCreditCommandHandler(db);
            await handler.Handle(
                new DeductTenantCreditCommand(orgId, amount, "lhdn:submit", key),
                CancellationToken.None);
        }

        var tasks = Enumerable.Range(0, 8).Select(_ => RunOnce()).ToArray();
        await Task.WhenAll(tasks);

        await using var verify = CreateDbContext(Guid.Empty);
        var wallet = await verify.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == orgId);
        wallet.AvailableCredits.Should().Be(75);

        var logs = await verify.CreditDeductionIdempotencyLogs.IgnoreQueryFilters()
            .Where(l => l.OrganizationId == orgId && l.IdempotencyKey == key)
            .ToListAsync();
        logs.Should().HaveCount(1);
    }

    [Test]
    public async Task Concurrent_DistinctKeys_CannotOverdraw()
    {
        RequirePostgres();
        // 10 concurrent deductions of 15 from a balance of 100 → at most 6 succeed (90), rest fail.
        var orgId = await SeedWalletAsync(100);
        const int amount = 15;
        var success = 0;
        var fail = 0;

        async Task Run(int i)
        {
            try
            {
                await using var db = CreateDbContext(Guid.Empty);
                var handler = new DeductTenantCreditCommandHandler(db);
                await handler.Handle(
                    new DeductTenantCreditCommand(orgId, amount, $"op-{i}", $"key-{i}"),
                    CancellationToken.None);
                Interlocked.Increment(ref success);
            }
            catch
            {
                Interlocked.Increment(ref fail);
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 10).Select(Run));

        await using var verify = CreateDbContext(Guid.Empty);
        var wallet = await verify.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == orgId);

        // Never overdraw; each success costs 15.
        wallet.AvailableCredits.Should().BeGreaterThanOrEqualTo(0);
        wallet.AvailableCredits.Should().Be(100 - success * amount);
        success.Should().BeInRange(1, 6);
        (success + fail).Should().Be(10);
        fail.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Sequential_SameKey_IsIdempotent_OnPostgres()
    {
        RequirePostgres();
        var orgId = await SeedWalletAsync(50);

        await using var db = CreateDbContext(Guid.Empty);
        var handler = new DeductTenantCreditCommandHandler(db);
        var cmd = new DeductTenantCreditCommand(orgId, 20, "retry-path", "seq-idem");

        await handler.Handle(cmd, CancellationToken.None);
        await handler.Handle(cmd, CancellationToken.None);

        var wallet = await db.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == orgId);
        wallet.AvailableCredits.Should().Be(30);
    }
}
