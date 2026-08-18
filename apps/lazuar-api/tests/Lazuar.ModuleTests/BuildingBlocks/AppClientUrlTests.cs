using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class AppClientUrlTests
{
    [Test]
    public void Missing_Config_Uses_Portal_Port_3004()
    {
        Assert.That(AppClientUrl.Resolve(null), Is.EqualTo("http://localhost:3004"));
        Assert.That(AppClientUrl.Resolve(new ConfigurationBuilder().Build()), Is.EqualTo("http://localhost:3004"));
    }

    [Test]
    public void Configured_Value_Is_Trimmed()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://pay.example/" })
            .Build();
        Assert.That(AppClientUrl.Resolve(config), Is.EqualTo("https://pay.example"));
    }
}
