using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.Configuration;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Domain;
using Modules.One.Infrastructure.Workers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class WebhookEndpointLifecycleTests
{
    private static ISecretVault CreateVault() =>
        new AesSecretVault(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kms:MasterKey"] = "test-master-key-for-unit-tests-32"
            })
            .Build());

    [Test]
    public async Task Create_First_Returns_Whsec_Once_And_Stores_Ciphertext()
    {
        var (repo, endpoints, tokens, vault) = Harness();
        var handler = new CreateWebhookEndpointCommandHandler(repo, tokens, vault);
        var orgId = Guid.CreateVersion7();

        var result = await handler.Handle(
            new CreateWebhookEndpointCommand(orgId, "https://aura.example/hooks"),
            CancellationToken.None);

        Assert.That(result.SecretKey, Does.StartWith("whsec_"));
        Assert.That(endpoints, Has.Count.EqualTo(1));
        Assert.That(endpoints[0].SecretKey, Does.Not.StartWith("whsec_"));
        Assert.That(vault.Decrypt(endpoints[0].SecretKey), Is.EqualTo(result.SecretKey));
    }

    [Test]
    public async Task Create_SameUrl_SecondCall_SecretKey_Null_SameId()
    {
        var (repo, endpoints, tokens, vault) = Harness();
        var handler = new CreateWebhookEndpointCommandHandler(repo, tokens, vault);
        var orgId = Guid.CreateVersion7();
        const string url = "https://aura.example/hooks";

        var first = await handler.Handle(new CreateWebhookEndpointCommand(orgId, url), CancellationToken.None);
        var second = await handler.Handle(new CreateWebhookEndpointCommand(orgId, url), CancellationToken.None);

        Assert.That(second.SecretKey, Is.Null);
        Assert.That(second.Id, Is.EqualTo(first.Id));
        Assert.That(endpoints, Has.Count.EqualTo(1));
        repo.Received(1).AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>());
    }

    [Test]
    public async Task Rotate_Returns_New_Whsec_And_Old_Cipher_Gone()
    {
        var (repo, endpoints, tokens, vault) = Harness();
        tokens.GenerateSecureToken(24).Returns(
            new GeneratedToken("abcdefghijklmnopqrstuvwx", "h1"),
            new GeneratedToken("zyxwvutsrqponmlkjihgfedc", "h2"));

        var create = new CreateWebhookEndpointCommandHandler(repo, tokens, vault);
        var orgId = Guid.CreateVersion7();
        var created = await create.Handle(
            new CreateWebhookEndpointCommand(orgId, "https://aura.example/hooks"),
            CancellationToken.None);

        var oldCipher = endpoints[0].SecretKey;
        var rotate = new RotateWebhookEndpointSecretCommandHandler(repo, tokens, vault);
        var rotated = await rotate.Handle(
            new RotateWebhookEndpointSecretCommand(orgId, created.Id),
            CancellationToken.None);

        Assert.That(rotated.SecretKey, Does.StartWith("whsec_"));
        Assert.That(rotated.SecretKey, Is.Not.EqualTo(created.SecretKey));
        Assert.That(endpoints[0].SecretKey, Is.Not.EqualTo(oldCipher));
        Assert.That(endpoints[0].SecretKey, Does.Not.StartWith("whsec_"));
        Assert.That(vault.Decrypt(endpoints[0].SecretKey), Is.EqualTo(rotated.SecretKey));
    }

    [Test]
    public async Task Disable_Sets_IsActive_False()
    {
        var (repo, endpoints, tokens, vault) = Harness();
        var create = new CreateWebhookEndpointCommandHandler(repo, tokens, vault);
        var orgId = Guid.CreateVersion7();
        var created = await create.Handle(
            new CreateWebhookEndpointCommand(orgId, "https://aura.example/hooks"),
            CancellationToken.None);

        Assert.That(endpoints[0].IsActive, Is.True);

        var disable = new DisableWebhookEndpointCommandHandler(repo);
        await disable.Handle(new DisableWebhookEndpointCommand(orgId, created.Id), CancellationToken.None);
        await disable.Handle(new DisableWebhookEndpointCommand(orgId, created.Id), CancellationToken.None);

        Assert.That(endpoints[0].IsActive, Is.False);
    }

    [Test]
    public void UrlValidator_Allows_Localhost_Http()
    {
        Assert.That(
            WebhookUrlValidator.NormalizeAndValidate("http://localhost:3000/hooks", allowHttpLoopback: true),
            Is.EqualTo("http://localhost:3000/hooks"));
        Assert.That(
            WebhookUrlValidator.NormalizeAndValidate("http://127.0.0.1/x", allowHttpLoopback: true),
            Is.EqualTo("http://127.0.0.1/x"));
    }

    [Test]
    public void Dispatcher_Decrypts_Encrypted_Secret_Before_Hmac()
    {
        var vault = CreateVault();
        const string plain = "whsec_signing_material";
        var endpoint = new TenantWebhookEndpoint(
            Guid.CreateVersion7(),
            "https://aura.example/hooks",
            vault.Encrypt(plain));

        var resolved = OutboundWebhookDispatcherJob.ResolveSigningSecret(vault, endpoint);
        Assert.That(resolved, Is.EqualTo(plain));
        Assert.That(endpoint.SecretKey, Does.Not.StartWith("whsec_"));
    }

    [Test]
    public void Dispatcher_LazyEncrypts_Plaintext_Whsec_Rows()
    {
        var vault = CreateVault();
        const string plain = "whsec_legacy_plaintext";
        var endpoint = new TenantWebhookEndpoint(
            Guid.CreateVersion7(),
            "https://aura.example/hooks",
            plain);

        var resolved = OutboundWebhookDispatcherJob.ResolveSigningSecret(vault, endpoint);
        Assert.That(resolved, Is.EqualTo(plain));
        Assert.That(endpoint.SecretKey, Does.Not.StartWith("whsec_"));
        Assert.That(vault.Decrypt(endpoint.SecretKey), Is.EqualTo(plain));
    }

    private static (
        IOneRepository Repo,
        List<TenantWebhookEndpoint> Endpoints,
        ITokenGeneratorService Tokens,
        ISecretVault Vault) Harness()
    {
        var endpoints = new List<TenantWebhookEndpoint>();
        var repo = Substitute.For<IOneRepository>();
        repo.When(r => r.AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>()))
            .Do(ci => endpoints.Add(ci.Arg<TenantWebhookEndpoint>()));
        repo.ListWebhookEndpointsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var orgId = ci.ArgAt<Guid>(0);
                IReadOnlyList<TenantWebhookEndpoint> list = endpoints
                    .Where(e => e.OrganizationId == orgId)
                    .ToList();
                return Task.FromResult(list);
            });
        repo.GetWebhookEndpointByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var id = ci.ArgAt<Guid>(0);
                return Task.FromResult(endpoints.FirstOrDefault(e => e.Id == id));
            });

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(24).Returns(new GeneratedToken("abcdefghijklmnopqrstuvwx", "h"));

        return (repo, endpoints, tokens, CreateVault());
    }
}
