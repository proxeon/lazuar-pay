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
}
