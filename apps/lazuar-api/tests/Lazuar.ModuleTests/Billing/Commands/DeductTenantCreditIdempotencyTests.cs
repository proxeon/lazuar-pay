using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Commands;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Commands;

/// <summary>
/// C.9 — Sequential credit deduct + idempotency (EF InMemory).
/// Concurrent xmin races require Postgres (see IntegrationTests CreditDeductionConcurrencyTests).
/// </summary>
[TestFixture]
public class DeductTenantCreditIdempotencyTests
{
    private BillingDbContext _db = null!;
    private Guid _orgId;

    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        _db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task SeedWalletAsync(int credits)
    {
        var wallet = new TenantCreditBalance(_orgId);
        if (credits > 0)
            wallet.TopUp(credits, "seed");
        _db.TenantCreditBalances.Add(wallet);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [Test]
    public async Task Deduct_WithSameIdempotencyKey_DoesNotDoubleCharge()
    {
        await SeedWalletAsync(100);
        var handler = new DeductTenantCreditCommandHandler(_db);
        var cmd = new DeductTenantCreditCommand(_orgId, 30, "lhdn:submit", "idem-1");

        await handler.Handle(cmd, CancellationToken.None);
        await handler.Handle(cmd, CancellationToken.None);
        await handler.Handle(cmd, CancellationToken.None);

        var wallet = await _db.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == _orgId);
        wallet.AvailableCredits.Should().Be(70);

        var logs = await _db.CreditDeductionIdempotencyLogs.IgnoreQueryFilters()
            .Where(l => l.OrganizationId == _orgId && l.IdempotencyKey == "idem-1")
            .ToListAsync();
        logs.Should().HaveCount(1);
        logs[0].Amount.Should().Be(30);
    }

    [Test]
    public async Task Deduct_WithDistinctKeys_ChargesEachOnce()
    {
        await SeedWalletAsync(100);
        var handler = new DeductTenantCreditCommandHandler(_db);

        await handler.Handle(new DeductTenantCreditCommand(_orgId, 10, "a", "key-a"), CancellationToken.None);
        await handler.Handle(new DeductTenantCreditCommand(_orgId, 15, "b", "key-b"), CancellationToken.None);
        await handler.Handle(new DeductTenantCreditCommand(_orgId, 10, "a-retry", "key-a"), CancellationToken.None);

        var wallet = await _db.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == _orgId);
        wallet.AvailableCredits.Should().Be(75); // 100 - 10 - 15

        var logCount = await _db.CreditDeductionIdempotencyLogs.IgnoreQueryFilters()
            .CountAsync(l => l.OrganizationId == _orgId);
        logCount.Should().Be(2);
    }

    [Test]
    public async Task Deduct_WithoutIdempotencyKey_AlwaysCharges()
    {
        await SeedWalletAsync(50);
        var handler = new DeductTenantCreditCommandHandler(_db);

        await handler.Handle(new DeductTenantCreditCommand(_orgId, 10, "raw-1", null), CancellationToken.None);
        await handler.Handle(new DeductTenantCreditCommand(_orgId, 10, "raw-2", null), CancellationToken.None);

        var wallet = await _db.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == _orgId);
        wallet.AvailableCredits.Should().Be(30);
    }

    [Test]
    public async Task Deduct_InsufficientBalance_Throws()
    {
        await SeedWalletAsync(5);
        var handler = new DeductTenantCreditCommandHandler(_db);

        var act = async () => await handler.Handle(
            new DeductTenantCreditCommand(_orgId, 10, "too-much", "idem-x"), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
        var wallet = await _db.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == _orgId);
        wallet.AvailableCredits.Should().Be(5);
    }
}
