using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class InMemoryEventBusTests
{
    [Test]
    public void Publish_With_No_Handlers_Throws()
    {
        var bus = CreateBus();
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => bus.PublishAsync(new NoteEvent()));
        Assert.That(ex!.Message, Does.Contain(nameof(NoteEvent)));
        Assert.That(ex.Message, Does.Contain("no registered handlers"));
    }

    [Test]
    public async Task Publish_With_Handler_Invokes_HandleAsync()
    {
        var bus = CreateBus();
        bus.Subscribe<NoteEvent, NoteHandler>();

        var note = new NoteEvent();
        await bus.PublishAsync(note);

        Assert.That(NoteHandler.LastSeen, Is.SameAs(note));
    }

    private static InMemoryEventBus CreateBus()
    {
        var services = new ServiceCollection();
        services.AddTransient<NoteHandler>();
        return new InMemoryEventBus(services.BuildServiceProvider(), NullLogger<InMemoryEventBus>.Instance);
    }

    private sealed record NoteEvent : IIntegrationEvent
    {
        public Guid Id { get; init; } = Guid.CreateVersion7();
        public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    }

    private sealed class NoteHandler : IIntegrationEventHandler<NoteEvent>
    {
        public static NoteEvent? LastSeen { get; private set; }

        public Task HandleAsync(NoteEvent @event)
        {
            LastSeen = @event;
            return Task.CompletedTask;
        }
    }
}
