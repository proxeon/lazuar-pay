using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.EventHandlers;
using Modules.One.Contracts;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class TenantUpdatedUnpublishProductsHandlerTests
{
    [Test]
    public async Task InactiveTenant_ArchivesLiveProducts()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = new CommerceDbContext(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var live = new Product(
            orgId, "Live", "live", 10m, "FIXED", 0m, "MYR", "one_time", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());
        db.Products.Add(live);
        await db.SaveChangesAsync();

        var handler = new TenantUpdatedUnpublishProductsHandler(db);
        await handler.HandleAsync(new TenantUpdatedIntegrationEvent(orgId, "Studio", "studio", false));

        var reloaded = await db.Products.IgnoreQueryFilters().SingleAsync();
        reloaded.IsActive.Should().BeFalse();
    }
}
