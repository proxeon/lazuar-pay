using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Communications.Contracts.Events;
using Modules.Communications.Infrastructure;
using Modules.Communications.Infrastructure.EventHandlers;
using Modules.One.Contracts;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class AppEntitlementGrantedIntegrationEventHandlerTests
{
    [Test]
    public async Task HandleAsync_CommerceNoTemplates_WritesDefaultTemplatesSeededToOutbox()
    {
        await using var db = new CommunicationsDbContext(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var tenantId = Guid.CreateVersion7();
        var handler = new AppEntitlementGrantedIntegrationEventHandler(
            db,
            new OutboxEventBus<CommunicationsDbContext>(db));

        await handler.HandleAsync(new AppEntitlementGrantedIntegrationEvent(tenantId, "COMMERCE"));

        (await db.MessageTemplates.IgnoreQueryFilters().CountAsync(t => t.OrganizationId == tenantId))
            .Should().BeGreaterThan(0);

        var row = await db.OutboxMessages.SingleAsync();
        row.Type.Should().Contain(nameof(DefaultTemplatesSeededIntegrationEvent));
        row.Data.Should().Contain(tenantId.ToString());
        row.ProcessedAt.Should().BeNull();
    }

    [Test]
    public async Task HandleAsync_CommerceAlreadySeeded_DoesNotPublishAgain()
    {
        await using var db = new CommunicationsDbContext(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var tenantId = Guid.CreateVersion7();
        var handler = new AppEntitlementGrantedIntegrationEventHandler(
            db,
            new OutboxEventBus<CommunicationsDbContext>(db));

        await handler.HandleAsync(new AppEntitlementGrantedIntegrationEvent(tenantId, "COMMERCE"));
        await handler.HandleAsync(new AppEntitlementGrantedIntegrationEvent(tenantId, "COMMERCE"));

        (await db.OutboxMessages.CountAsync()).Should().Be(1);
    }
}
