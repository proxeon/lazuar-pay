using Lazuar.Pay.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Tests;

public class PayBootTests
{
    [Test]
    public void Production_empty_wrap_key_throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Pay"] = "Host=db",
            ["One:BaseUrl"] = "https://one.example/api/v1"
        }).Build();
        var ex = Assert.Throws<InvalidOperationException>(() => PayBoot.ThrowIfMisconfigured(config, new NamedEnv("Production")));
        Assert.That(ex!.Message, Does.Contain("WrapKey"));
    }

    [Test]
    public void Production_empty_cs_throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pay:WrapKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            ["One:BaseUrl"] = "https://one.example/api/v1"
        }).Build();
        var ex = Assert.Throws<InvalidOperationException>(() => PayBoot.ThrowIfMisconfigured(config, new NamedEnv("Production")));
        Assert.That(ex!.Message, Does.Contain("ConnectionStrings:Pay"));
    }

    [Test]
    public void Production_localhost_one_url_throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pay:WrapKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            ["ConnectionStrings:Pay"] = "Host=db",
            ["One:BaseUrl"] = "http://localhost:8080/api/v1"
        }).Build();
        var ex = Assert.Throws<InvalidOperationException>(() => PayBoot.ThrowIfMisconfigured(config, new NamedEnv("Production")));
        Assert.That(ex!.Message, Does.Contain("One:BaseUrl"));
    }

    [Test]
    public void Testing_allows_empty()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        Assert.DoesNotThrow(() => PayBoot.ThrowIfMisconfigured(config, new NamedEnv("Testing")));
    }

    sealed class NamedEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
