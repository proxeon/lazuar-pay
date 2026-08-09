using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Modules.Communications.Domain.Aggregates;
using Modules.Communications.Infrastructure;
using Modules.Communications.Infrastructure.Workers;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class BroadcastClaimTests
{
    [Test]
    public async Task ClaimQueued_MarksSending_AndSkipsAlreadySending()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        var queued = new Broadcast(orgId, "Hello", "<p>body</p>");
        queued.Queue(10);

        var alreadySending = new Broadcast(orgId, "Other", "<p>body</p>");
        alreadySending.Queue(5);
        alreadySending.MarkSending();

        db.Broadcasts.AddRange(queued, alreadySending);
        await db.SaveChangesAsync();

        var claimed = await BroadcastFanoutJob.ClaimQueuedBroadcastsAsync(db, CancellationToken.None);

        claimed.Should().ContainSingle(b => b.Id == queued.Id);
        claimed[0].Status.Should().Be("SENDING");

        var second = await BroadcastFanoutJob.ClaimQueuedBroadcastsAsync(db, CancellationToken.None);
        second.Should().BeEmpty();
    }

    private static CommunicationsDbContext CreateDb()
        => new(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
