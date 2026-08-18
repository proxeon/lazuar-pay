using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class DeadLetterRedriveTests
{
    [Test]
    public async Task Reset_Clears_Dead_Outbox_And_Inbox()
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new CommerceDbContext(
            options,
            FakeExecutionContextAccessor.EmptyTenant(),
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());

        db.OutboxMessages.Add(new OutboxMessage
        {
            Status = MessageProcessingStatus.Dead,
            ProcessedAt = DateTime.UtcNow,
            AttemptCount = 5,
            Error = "boom",
        });
        db.InboxMessages.Add(new InboxMessage
        {
            Status = MessageProcessingStatus.Dead,
            ProcessedAt = DateTime.UtcNow,
            AttemptCount = 5,
        });
        db.OutboxMessages.Add(new OutboxMessage { Status = MessageProcessingStatus.Pending });
        await db.SaveChangesAsync();

        var reset = DeadLetterRedrive.Reset(db);
        await db.SaveChangesAsync();

        Assert.That(reset, Is.EqualTo(2));
        Assert.That(await db.OutboxMessages.CountAsync(m => m.Status == MessageProcessingStatus.Dead), Is.EqualTo(0));
        Assert.That(await db.InboxMessages.CountAsync(m => m.Status == MessageProcessingStatus.Dead), Is.EqualTo(0));
        Assert.That(await db.OutboxMessages.CountAsync(m => m.Status == MessageProcessingStatus.Pending), Is.EqualTo(2));
    }
}
