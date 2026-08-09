using System.Collections.Generic;
using System.Linq;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Billing.Infrastructure;
using Modules.Commerce.Infrastructure;
using Modules.Communications.Infrastructure;
using Modules.CRM.Infrastructure;
using Modules.Lhdn.Infrastructure;
using Modules.Lhdn.Infrastructure.Observability;
using Modules.Messaging.Infrastructure;
using Modules.One.Infrastructure;
using Modules.Ops.Infrastructure;
using Modules.Payments.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Observability;

[TestFixture]
public class PlatformMetricsPluginRegistrationTests
{
    private static readonly string[] ExpectedSchemas =
    [
        "billing",
        "commerce",
        "communications",
        "crm",
        "lhdn",
        "messaging",
        "one",
        "ops",
        "payments"
    ];

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=lazuar_test;Username=test;Password=test",
                ["ConnectionStrings:MessagingConnection"] = "Host=localhost;Database=lazuar_test;Username=test;Password=test",
                ["Observability:LhdnStuckThreshold"] = "02:00:00",
                ["Lhdn:ReferenceDataPath"] = "lhdn_docs/codes"
            })
            .Build();

    [Test]
    public void AddAllModules_Registers_Nine_OutboxSchemaMetrics()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfig();

        services.AddOneModule(configuration);
        services.AddMessagingModule(configuration);
        services.AddCrmModule(configuration);
        services.AddPaymentsModule(configuration);
        services.AddOpsModule(configuration);
        services.AddBillingModule(configuration);
        services.AddLhdnModule(configuration);
        services.AddCommerceModule(configuration);
        services.AddCommunicationsModule(configuration);

        var schemas = services
            .Where(d => d.ServiceType == typeof(IOutboxSchemaRegistration))
            .Select(d =>
            {
                if (d.ImplementationInstance is IOutboxSchemaRegistration reg)
                {
                    return reg.Schema;
                }

                return null;
            })
            .Where(s => s is not null)
            .Cast<string>()
            .OrderBy(s => s)
            .ToArray();

        Assert.That(schemas, Is.EquivalentTo(ExpectedSchemas));
    }

    [Test]
    public void AddLhdnModule_Registers_StuckContributor_And_Schema()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLhdnModule(BuildConfig());

        var schemaRegistered = services.Any(d =>
            d.ServiceType == typeof(IOutboxSchemaRegistration) &&
            d.ImplementationInstance is IOutboxSchemaRegistration { Schema: "lhdn" });

        var contributorRegistered = services.Any(d =>
            d.ServiceType == typeof(IPlatformMetricsContributor) &&
            d.ImplementationType == typeof(LhdnStuckMetricsContributor));

        Assert.That(schemaRegistered, Is.True, "AddLhdnModule must register outbox schema 'lhdn'.");
        Assert.That(contributorRegistered, Is.True,
            "AddLhdnModule must register LhdnStuckMetricsContributor as IPlatformMetricsContributor.");
    }

    [Test]
    public void AddOutboxSchemaMetrics_Rejects_Unsafe_Schema_Identifier()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddOutboxSchemaMetrics("lhdn\"; DROP TABLE"));
        Assert.Throws<ArgumentException>(() => services.AddOutboxSchemaMetrics("Lhdn"));
        Assert.Throws<ArgumentException>(() => services.AddOutboxSchemaMetrics(""));
    }

    [Test]
    public void AddOutboxSchemaMetrics_Accepts_Valid_Schema()
    {
        var services = new ServiceCollection();
        services.AddOutboxSchemaMetrics("one");

        var reg = services
            .Select(d => d.ImplementationInstance)
            .OfType<IOutboxSchemaRegistration>()
            .Single();

        Assert.That(reg.Schema, Is.EqualTo("one"));
    }
}
