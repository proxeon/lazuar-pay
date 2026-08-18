using System;
using Lazuar.Api.Composition;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class CorsOriginsGuardTests
{
    [Test]
    public void Production_Empty_Origins_Throws()
    {
        var env = new FakeEnv(Environments.Production);
        Assert.That(
            () => AuthAndCorsExtensions.EnsureCorsOriginsConfigured("", env),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("App:CorsOrigins"));
        Assert.That(
            () => AuthAndCorsExtensions.EnsureCorsOriginsConfigured("   ", env),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void Staging_Empty_Origins_Throws()
    {
        var env = new FakeEnv("Staging");
        Assert.That(
            () => AuthAndCorsExtensions.EnsureCorsOriginsConfigured(null, env),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void Development_Empty_Origins_Is_Allowed()
    {
        Assert.DoesNotThrow(() =>
            AuthAndCorsExtensions.EnsureCorsOriginsConfigured("", new FakeEnv(Environments.Development)));
    }

    [Test]
    public void Production_Configured_Origins_Is_Allowed()
    {
        Assert.DoesNotThrow(() =>
            AuthAndCorsExtensions.EnsureCorsOriginsConfigured(
                "https://hub.lazuar.com",
                new FakeEnv(Environments.Production)));
        Assert.That(
            AuthAndCorsExtensions.TryParseCorsOrigins("https://hub.lazuar.com, https://admin.lazuar.com", out var origins),
            Is.True);
        Assert.That(origins, Is.EqualTo(new[] { "https://hub.lazuar.com", "https://admin.lazuar.com" }));
    }

    private sealed class FakeEnv : IHostEnvironment
    {
        public FakeEnv(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Lazuar.Api";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
