using Lazuar.Pay.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Tests;

public class SecretBoxTests
{
    sealed class StubEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "pay";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Test]
    public void Production_missing_wrap_key_throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var box = new SecretBox(config, new StubEnv("Production"));
        var ex = Assert.Throws<InvalidOperationException>(() => box.Protect("x"));
        Assert.That(ex!.Message, Does.Contain("Pay:WrapKey"));
    }

    [Test]
    public void Testing_allows_dev_wrap_key()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var box = new SecretBox(config, new StubEnv("Testing"));
        var wrapped = box.Protect("x");
        Assert.That(box.Unprotect(wrapped), Is.EqualTo("x"));
    }
}
