using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Domain;

[TestFixture]
public class LedgerUniquePerTenantTests
{
    [Test]
    public async Task TwoOrgs_CanShareTheSameReferenceTypeAndId()
    {
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var a = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "bill_shared", "a");
        a.AddLine(AccountTypes.AssetCash, 10m, "MYR", 10m, "MYR");
        a.AddLine(AccountTypes.RevenueGross, -10m, "MYR", -10m, "MYR");
        a.ValidateBalanced();
        var b = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "bill_shared", "b");
        b.AddLine(AccountTypes.AssetCash, 10m, "MYR", 10m, "MYR");
        b.AddLine(AccountTypes.RevenueGross, -10m, "MYR", -10m, "MYR");
        b.ValidateBalanced();

        db.LedgerEntries.AddRange(a, b);
        await db.SaveChangesAsync();

        (await db.LedgerEntries.IgnoreQueryFilters().CountAsync(e => e.ReferenceId == "bill_shared"))
            .Should().Be(2);
    }

    [Test]
    public async Task HasEntryBeenProcessed_DoesNotSeeAnotherTenant()
    {
        await using var db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var orgA = Guid.CreateVersion7();
        var orgB = Guid.CreateVersion7();
        var a = new LedgerEntry(orgA, LedgerReferenceTypes.GatewayPayment, "pi_shared", "a");
        a.AddLine(AccountTypes.AssetCash, 10m, "MYR", 10m, "MYR");
        a.AddLine(AccountTypes.RevenueGross, -10m, "MYR", -10m, "MYR");
        a.ValidateBalanced();
        db.LedgerEntries.Add(a);
        await db.SaveChangesAsync();

        var repo = new Modules.Billing.Infrastructure.Repositories.LedgerRepository(db);
        (await repo.HasEntryBeenProcessedAsync(orgA, LedgerReferenceTypes.GatewayPayment, "pi_shared"))
            .Should().BeTrue();
        (await repo.HasEntryBeenProcessedAsync(orgB, LedgerReferenceTypes.GatewayPayment, "pi_shared"))
            .Should().BeFalse();
    }
}
