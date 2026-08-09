using System.Collections.Generic;
using System.Linq;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modules.CRM.Infrastructure;
using Modules.CRM.Infrastructure.Workers;
using NUnit.Framework;

namespace Lazuar.ModuleTests.CRM;

[TestFixture]
public class CrmOutboxInboxRegistrationTests
{
    [Test]
    public void AddCrmModule_Registers_OutboxBus_And_ThinJobSubclasses_Via_Helper()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=lazuar_test;Username=test;Password=test"
            })
            .Build();

        services.AddCrmModule(configuration);

        var outboxJob = services.Any(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(CrmOutboxPublisherJob));
        var inboxJob = services.Any(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(CrmInboxConsumerJob));

        Assert.That(outboxJob, Is.True,
            "AddCrmModule must register CrmOutboxPublisherJob as IHostedService (thin subclass preserved).");
        Assert.That(inboxJob, Is.True,
            "AddCrmModule must register CrmInboxConsumerJob as IHostedService (thin subclass preserved).");

        // Keyed OutboxEventBus registered under CrmEventBus (Option A helper).
        var keyedBus = services.Any(d =>
            d.ServiceType == typeof(IEventBus) &&
            d.IsKeyedService &&
            Equals(d.ServiceKey, "CrmEventBus") &&
            d.KeyedImplementationType == typeof(OutboxEventBus<CrmDbContext>));

        Assert.That(keyedBus, Is.True,
            "AddModuleOutboxInbox must register keyed OutboxEventBus<CrmDbContext> as CrmEventBus.");
    }
}
