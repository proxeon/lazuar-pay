using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Workers;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce.Workers;

[TestFixture]
public class InvoiceReminderJobTests
{
    private CommerceDbContext _db = null!;
    private ServiceProvider _sp = null!;
    private IEventBus _eventBus = null!;
    private InvoiceReminderJob _job = null!;
    private Guid _orgId;

    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        _db = new CommerceDbContext(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());
        _eventBus = Substitute.For<IEventBus>();
        var one = Substitute.For<IOneQueryService>();
        one.GetWorkspaceByIdAsync(_orgId).Returns(new WorkspaceSnapshotDto(_orgId, "Acme", "acme", true, DateTime.UtcNow));

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddKeyedSingleton<IEventBus>("CommerceEventBus", _eventBus);
        services.AddSingleton(one);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "http://localhost:3004" })
            .Build());
        _sp = services.BuildServiceProvider();
        _job = new InvoiceReminderJob(_sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<InvoiceReminderJob>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _sp.Dispose();
    }

    [Test]
    public async Task Day0Due_OpenCustom_SendsOnce()
    {
        var session = CustomOpen(_orgId, DateTime.UtcNow);
        _db.CheckoutSessions.Add(session);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync();
        await _job.RunOnceAsync();

        (await _db.InvoiceReminderDispatchLogs.CountAsync(l => l.SessionId == session.Id && l.DayOffset == 0))
            .Should().Be(1);
        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "invoice.reminder"
            && e.InternalTargetApp == "COMMUNICATIONS"
            && e.Payload.GetProperty("checkout_url").GetString()!.Contains($"/acme/pay/{session.Id}")));
    }

    [Test]
    public async Task Completed_IsSkipped()
    {
        var session = CustomOpen(_orgId, DateTime.UtcNow);
        session.Complete();
        _db.CheckoutSessions.Add(session);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync();

        (await _db.InvoiceReminderDispatchLogs.CountAsync()).Should().Be(0);
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
    }

    [Test]
    public async Task ProductSession_IsIgnored()
    {
        var session = new CheckoutSession(
            _orgId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            DateTime.UtcNow.AddDays(7));
        session.SetDueAt(DateTime.UtcNow);
        _db.CheckoutSessions.Add(session);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync();

        (await _db.InvoiceReminderDispatchLogs.CountAsync()).Should().Be(0);
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
    }

    private static CheckoutSession CustomOpen(Guid orgId, DateTime dueAt)
    {
        var session = new CheckoutSession(
            orgId,
            Guid.CreateVersion7(),
            new[] { new AdHocLineItem("Work", 1, 100m) },
            DateTime.UtcNow.AddDays(30),
            false);
        session.SetDueAt(dueAt);
        return session;
    }
}
