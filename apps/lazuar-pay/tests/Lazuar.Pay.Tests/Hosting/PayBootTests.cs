using Lazuar.Pay.Hosting;
using Lazuar.Pay.Rails.Solana;
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

    [Test]
    public void Production_devnet_cluster_throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pay:WrapKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            ["ConnectionStrings:Pay"] = "Host=db",
            ["One:BaseUrl"] = "https://one.example/api/v1",
            ["Pay:CheckoutBaseUrl"] = "https://checkout.example",
            ["Pay:CorsOrigins"] = "https://checkout.example",
            ["Pay:Solana:Cluster"] = "devnet",
            ["Pay:Solana:RpcUrl"] = "https://rpc.example/devnet"
        }).Build();
        var ex = Assert.Throws<InvalidOperationException>(() => PayBoot.ThrowIfMisconfigured(config, new NamedEnv("Production")));
        Assert.That(ex!.Message, Does.Contain("mainnet-beta"));
    }

    [Test]
    public void Production_public_solana_rpc_throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pay:WrapKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            ["ConnectionStrings:Pay"] = "Host=db",
            ["One:BaseUrl"] = "https://one.example/api/v1",
            ["Pay:CheckoutBaseUrl"] = "https://checkout.example",
            ["Pay:CorsOrigins"] = "https://checkout.example",
            ["Pay:Solana:Cluster"] = "mainnet-beta",
            ["Pay:Solana:RpcUrl"] = "https://api.mainnet-beta.solana.com"
        }).Build();
        var ex = Assert.Throws<InvalidOperationException>(() => PayBoot.ThrowIfMisconfigured(config, new NamedEnv("Production")));
        Assert.That(ex!.Message, Does.Contain("RpcUrl"));
    }

    [Test]
    public void Production_checkout_origin_must_be_in_cors()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pay:WrapKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            ["ConnectionStrings:Pay"] = "Host=db",
            ["One:BaseUrl"] = "https://one.example/api/v1",
            ["Pay:CheckoutBaseUrl"] = "https://checkout.example",
            ["Pay:CorsOrigins"] = "https://merchant.example"
        }).Build();
        var ex = Assert.Throws<InvalidOperationException>(() => PayBoot.ThrowIfMisconfigured(config, new NamedEnv("Production")));
        Assert.That(ex!.Message, Does.Contain("CorsOrigins"));
    }

    [Test]
    public void Production_http_cors_origin_throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pay:WrapKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            ["ConnectionStrings:Pay"] = "Host=db",
            ["One:BaseUrl"] = "https://one.example/api/v1",
            ["Pay:CheckoutBaseUrl"] = "https://checkout.example",
            ["Pay:CorsOrigins"] = "http://checkout.example"
        }).Build();
        var ex = Assert.Throws<InvalidOperationException>(() => PayBoot.ThrowIfMisconfigured(config, new NamedEnv("Production")));
        Assert.That(ex!.Message, Does.Contain("https"));
    }

    [Test]
    public void Genesis_hash_is_pinned_per_cluster()
    {
        Assert.That(SolanaCluster.ParseGenesisHash("""{"jsonrpc":"2.0","result":"5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d"}"""),
            Is.EqualTo(SolanaCluster.MainnetGenesis));
        Assert.That(SolanaCluster.GenesisHash(SolanaCluster.Devnet), Is.EqualTo(SolanaCluster.DevnetGenesis));
        Assert.That(SolanaCluster.MatchesVault(SolanaCluster.Devnet, "devnet"));
        Assert.That(SolanaCluster.MatchesVault(SolanaCluster.Mainnet, "mainnet"));
        Assert.That(SolanaCluster.MatchesVault(SolanaCluster.Devnet, "mainnet"), Is.False);
    }

    sealed class NamedEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
