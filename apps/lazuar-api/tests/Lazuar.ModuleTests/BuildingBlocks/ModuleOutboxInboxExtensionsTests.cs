using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class ModuleOutboxInboxExtensionsTests
{
    private sealed class PilotDbContext : DbContext
    {
        public PilotDbContext(DbContextOptions<PilotDbContext> options) : base(options) { }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyOutboxInbox();
        }
    }

    private sealed class PilotOutboxJob : OutboxPublisherJob<PilotDbContext>
    {
        public PilotOutboxJob(
            IServiceScopeFactory scopeFactory,
            ILogger<PilotOutboxJob> logger,
            DatabaseJobTrigger jobTrigger)
            : base(scopeFactory, logger, jobTrigger)
        {
        }
    }

    private sealed class PilotInboxJob : InboxConsumerJob<PilotDbContext>
    {
        public PilotInboxJob(
            IServiceScopeFactory scopeFactory,
            ILogger<PilotInboxJob> logger,
            DatabaseJobTrigger jobTrigger)
            : base(scopeFactory, logger, jobTrigger)
        {
        }
    }

    [Test]
    public void AddModuleOutboxInbox_Registers_Bus_And_Both_Jobs()
    {
        var services = new ServiceCollection();

        services.AddModuleOutboxInbox<PilotDbContext, PilotOutboxJob, PilotInboxJob>("PilotEventBus");

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(PilotOutboxJob));
        services.Should().Contain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(PilotInboxJob));
        services.Should().Contain(d =>
            d.IsKeyedService &&
            Equals(d.ServiceKey, "PilotEventBus"));
    }

    [Test]
    public void ApplyOutboxInbox_Configures_Pending_Indexes_And_Tables()
    {
        var options = new DbContextOptionsBuilder<PilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new PilotDbContext(options);
        var model = db.Model;

        var outbox = model.FindEntityType(typeof(OutboxMessage));
        outbox.Should().NotBeNull();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType outboxEntity = outbox!;
        outboxEntity.GetTableName().Should().Be("OutboxMessages");
        outboxEntity.FindPrimaryKey().Should().NotBeNull();

        var outboxIndex = outboxEntity.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "NextAttemptAt", "OccurredOn" }));
        outboxIndex.GetFilter().Should().Be("\"ProcessedAt\" IS NULL");

        var inbox = model.FindEntityType(typeof(InboxMessage));
        inbox.Should().NotBeNull();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType inboxEntity = inbox!;
        inboxEntity.GetTableName().Should().Be("InboxMessages");

        var inboxIndex = inboxEntity.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "NextAttemptAt", "ReceivedAt" }));
        inboxIndex.GetFilter().Should().Be("\"ProcessedAt\" IS NULL");
    }
}
