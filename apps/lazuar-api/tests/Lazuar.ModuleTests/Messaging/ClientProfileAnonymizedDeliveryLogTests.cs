using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.CRM.Contracts;
using Modules.Messaging.Domain;
using Modules.Messaging.Infrastructure;
using Modules.Messaging.Infrastructure.EventHandlers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Messaging;

[TestFixture]
public class ClientProfileAnonymizedDeliveryLogTests
{
    [Test]
    public async Task HandleAsync_Scrubs_Matching_Recipient()
    {
        var orgId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        await using var db = new MessagingDbContext(
            options,
            Substitute.For<IExecutionContextAccessor>(),
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());

        db.MessageDeliveryLogs.Add(new MessageDeliveryLog(orgId, "EMAIL", "buyer@example.com", "SENT", "re_1"));
        db.MessageDeliveryLogs.Add(new MessageDeliveryLog(orgId, "EMAIL", "other@example.com", "SENT", "re_2"));
        await db.SaveChangesAsync();

        var handler = new ClientProfileAnonymizedIntegrationEventHandler(db);
        await handler.HandleAsync(new ClientProfileAnonymizedIntegrationEvent(
            orgId, profileId, "Buyer@Example.com", null));

        var rows = await db.MessageDeliveryLogs.IgnoreQueryFilters().OrderBy(l => l.ProviderMessageId).ToListAsync();
        rows[0].Recipient.Should().Be($"deleted_{profileId}@localhost");
        rows[0].ProviderMessageId.Should().Be("re_1");
        rows[1].Recipient.Should().Be("other@example.com");
    }
}
