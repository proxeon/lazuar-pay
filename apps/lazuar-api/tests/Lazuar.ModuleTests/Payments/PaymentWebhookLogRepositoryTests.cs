using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Modules.Payments.Application.Ports;
using Modules.Payments.Domain.Entities;
using Modules.Payments.Infrastructure;
using Modules.Payments.Infrastructure.Repositories;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class PaymentWebhookLogRepositoryTests
{
    private PaymentsDbContext _db = null!;
    private PaymentWebhookLogRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new PaymentsDbContext(
            InMemoryDb.CreateOptions<PaymentsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
        _repo = new PaymentWebhookLogRepository(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task TryRequeueDeadOutbox_Dead_BecomesPending()
    {
        var id = Guid.CreateVersion7();
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            Type = "test",
            Data = "{}",
            Status = MessageProcessingStatus.Dead,
            ProcessedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(10),
            AttemptCount = 5
        });
        await _db.SaveChangesAsync();

        var result = await _repo.TryRequeueDeadOutboxAsync(id);

        result.Should().Be(OutboxRequeueResult.Requeued);
        var row = await _db.OutboxMessages.FindAsync(id);
        row!.Status.Should().Be(MessageProcessingStatus.Pending);
        row.ProcessedAt.Should().BeNull();
        row.NextAttemptAt.Should().BeNull();
        row.AttemptCount.Should().Be(0);
    }

    [Test]
    public async Task TryRequeueDeadOutbox_Pending_IsAlreadyActive()
    {
        var id = Guid.CreateVersion7();
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            Type = "test",
            Data = "{}",
            Status = MessageProcessingStatus.Pending,
            AttemptCount = 1
        });
        await _db.SaveChangesAsync();

        var result = await _repo.TryRequeueDeadOutboxAsync(id);

        result.Should().Be(OutboxRequeueResult.AlreadyActive);
        (await _db.OutboxMessages.FindAsync(id))!.AttemptCount.Should().Be(1);
    }

    [Test]
    public async Task TryRequeueDeadOutbox_UnknownId_IsMissing()
    {
        var result = await _repo.TryRequeueDeadOutboxAsync(Guid.CreateVersion7());
        result.Should().Be(OutboxRequeueResult.Missing);
    }

    [Test]
    public async Task GetByEventId_ReturnsTrackedLog()
    {
        var log = new PaymentWebhookLog("evt_1", "STRIPE", "PAYMENT_COMPLETED:pi_1", Guid.CreateVersion7());
        _repo.Add(log);
        await _repo.SaveChangesAsync();

        var found = await _repo.GetByEventIdAsync("evt_1", "STRIPE");
        found.Should().NotBeNull();
        found!.BusinessKey.Should().Be("PAYMENT_COMPLETED:pi_1");
        found.OutboxMessageId.Should().Be(log.OutboxMessageId);
    }
}
