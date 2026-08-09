using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.TestSupport;

/// <summary>
/// Thin helpers for unique InMemory database options. Callers still construct module DbContexts
/// (constructors differ per module) and pass <see cref="FakeExecutionContextAccessor"/> + mediator/trigger.
/// </summary>
public static class InMemoryDb
{
    /// <summary>Unique database name so parallel tests do not share state.</summary>
    public static DbContextOptions<TContext> CreateOptions<TContext>()
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    /// <summary>
    /// Mediator that no-ops <c>Publish</c> (domain events during SaveChanges) and fails loud on <c>Send</c>/<c>CreateStream</c>.
    /// </summary>
    public static IMediator NullMediator { get; } = new NullMediatorImpl();

    private sealed class NullMediatorImpl : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw SendNotSupported();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw SendNotSupported();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw SendNotSupported();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            => throw SendNotSupported();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw SendNotSupported();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;

        private static InvalidOperationException SendNotSupported([CallerMemberName] string? member = null)
            => new(
                $"NullMediator does not handle {member}. Use a real mediator or NSubstitute when the SUT dispatches commands.");
    }
}
