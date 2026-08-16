using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Communications.Infrastructure;
using Modules.Communications.Infrastructure.EventHandlers;
using Modules.Communications.Infrastructure.Services;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class ClientProfileAnonymizedSuppressionTests
{
    [Test]
    public async Task HandleAsync_SuppressesPreWipeEmailAsAnonymized()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = CreateDb(orgId);
        var handler = new ClientProfileAnonymizedIntegrationEventHandler(
            new SuppressionService(db),
            Substitute.For<ILogger<ClientProfileAnonymizedIntegrationEventHandler>>());

        await handler.HandleAsync(new ClientProfileAnonymizedIntegrationEvent(
            orgId, Guid.CreateVersion7(), "Buyer@Example.com", "60123456789"));

        var entry = await db.SuppressionEntries.IgnoreQueryFilters().SingleAsync();
        entry.OrganizationId.Should().Be(orgId);
        entry.Email.Should().Be("buyer@example.com");
        entry.Reason.Should().Be("ANONYMIZED");
        entry.Source.Should().Be("gdpr_client_profile_anonymized");
    }

    [Test]
    public async Task HandleAsync_DummyEmail_DoesNotSuppress()
    {
        var orgId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        await using var db = CreateDb(orgId);
        var handler = new ClientProfileAnonymizedIntegrationEventHandler(
            new SuppressionService(db),
            Substitute.For<ILogger<ClientProfileAnonymizedIntegrationEventHandler>>());

        await handler.HandleAsync(new ClientProfileAnonymizedIntegrationEvent(
            orgId, profileId, $"deleted_{profileId}@localhost", null));

        (await db.SuppressionEntries.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task HandleAsync_MissingEmail_DoesNotSuppress()
    {
        var orgId = Guid.CreateVersion7();
        await using var db = CreateDb(orgId);
        var handler = new ClientProfileAnonymizedIntegrationEventHandler(
            new SuppressionService(db),
            Substitute.For<ILogger<ClientProfileAnonymizedIntegrationEventHandler>>());

        await handler.HandleAsync(new ClientProfileAnonymizedIntegrationEvent(
            orgId, Guid.CreateVersion7(), null, "601"));

        (await db.SuppressionEntries.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    private static CommunicationsDbContext CreateDb(Guid orgId) =>
        new(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.ForTenant(orgId),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
