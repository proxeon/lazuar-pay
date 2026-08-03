using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modules.Lhdn.Infrastructure;
using Modules.Lhdn.Infrastructure.Workers;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class LhdnOutboxPublisherJobRegistrationTests
{
    [Test]
    public void AddLhdnModule_Registers_LhdnOutboxPublisherJob_As_HostedService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=lazuar_test;Username=test;Password=test"
            })
            .Build();

        services.AddLhdnModule(configuration);

        var registered = services.Any(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(LhdnOutboxPublisherJob));

        Assert.That(registered, Is.True,
            "AddLhdnModule must register LhdnOutboxPublisherJob as IHostedService so outbox rows drain.");
    }
}
