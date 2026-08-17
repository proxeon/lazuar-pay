using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Modules.Commerce.Application;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Repositories;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CouponLockTransactionTests
{
    [Test]
    public void CommerceRepository_IsTransactional()
    {
        typeof(CommerceRepository).Should().Implement<ICommerceTransactional>();
        typeof(ICommerceRepository).Should().NotBeAssignableTo<ICommerceTransactional>();
    }

    [Test]
    public async Task ExecuteInTransactionAsync_RunsAction()
    {
        await using var db = CreateDb();
        var repo = new CommerceRepository(db);
        var ran = false;

        await repo.ExecuteInTransactionAsync(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        ran.Should().BeTrue();
    }

    private static CommerceDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
