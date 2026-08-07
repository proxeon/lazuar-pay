using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.Api.Middleware;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Http;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Application.Queries;
using Modules.One.Contracts.Events;
using Modules.One.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class GenerateAndListApiCredentialsTests
{
    [Test]
    public async Task GenerateApiCredential_Returns_Rich_Result_With_Hint_And_Scopes()
    {
        var repo = Substitute.For<IOneRepository>();
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("abcdefghij1234567890abcdefghij1234567890", "hash-of-random"));
        tokens.HashToken(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");

        ApiCredential? saved = null;
        repo.When(r => r.AddApiCredential(Arg.Any<ApiCredential>()))
            .Do(ci => saved = ci.Arg<ApiCredential>());

        var handler = new GenerateApiCredentialCommandHandler(repo, tokens);
        var orgId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var result = await handler.Handle(
            new GenerateApiCredentialCommand(orgId, "Integration", IsTestMode: true, CreatedByUserId: userId),
            CancellationToken.None);

        Assert.That(result.Name, Is.EqualTo("Integration"));
        Assert.That(result.Prefix, Is.EqualTo("sk_test_"));
        Assert.That(result.PlainKey, Does.StartWith("sk_test_"));
        Assert.That(result.Hint, Is.EqualTo(result.PlainKey[^4..]));
        Assert.That(result.Scopes, Is.EqualTo(PlatformApiScopes.DefaultDocumentScopes));
        Assert.That(result.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.KeyHint, Is.EqualTo(result.Hint));
        Assert.That(saved.Scopes, Is.EqualTo(PlatformApiScopes.DefaultDocumentScopes));
        Assert.That(saved.OrganizationId, Is.EqualTo(orgId));
        Assert.That(saved.CreatedByUserId, Is.EqualTo(userId));
        // Persist hash of the full plain key only (no separate plaintext column on the aggregate).
        Assert.That(saved.KeyHash, Is.EqualTo($"hash:{result.PlainKey}"));
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Generate_Shows_PlainKey_Once_But_List_Never_Returns_Secret()
    {
        var orgId = Guid.CreateVersion7();
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("onceonlysecretabcdefghij1234567890ab", "unused"));
        tokens.HashToken(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");

        ApiCredential? saved = null;
        var repo = Substitute.For<IOneRepository>();
        repo.When(r => r.AddApiCredential(Arg.Any<ApiCredential>()))
            .Do(ci => saved = ci.Arg<ApiCredential>());

        var generate = new GenerateApiCredentialCommandHandler(repo, tokens);
        var created = await generate.Handle(
            new GenerateApiCredentialCommand(orgId, "Once", IsTestMode: false),
            CancellationToken.None);

        Assert.That(created.PlainKey, Does.StartWith("sk_live_"));
        Assert.That(created.PlainKey.Length, Is.GreaterThan(20));

        // List path only sees the stored aggregate (hash + hint), never PlainKey.
        repo.ListApiCredentialsAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ApiCredential>>(new List<ApiCredential> { saved! }));

        var listHandler = new ListApiCredentialsQueryHandler(repo);
        var list = (await listHandler.Handle(new ListApiCredentialsQuery(orgId), CancellationToken.None)).ToList();

        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Hint, Is.EqualTo(created.Hint));
        Assert.That(list[0].Prefix, Is.EqualTo("sk_live_"));

        // Contract DTO has no secret field.
        Assert.That(typeof(ApiKeyDto).GetProperty("Plain_key", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        Assert.That(typeof(ApiKeyDto).GetProperty("PlainKey", BindingFlags.Public | BindingFlags.Instance), Is.Null);

        var json = JsonSerializer.Serialize(list[0]);
        Assert.That(json, Does.Not.Contain(created.PlainKey));
        Assert.That(json, Does.Not.Contain("plain_key").IgnoreCase);
        Assert.That(json, Does.Contain("hint"));
    }

    [Test]
    public async Task ListApiCredentials_Returns_Metadata_Without_Secret()
    {
        var orgId = Guid.CreateVersion7();
        var key = new ApiCredential(
            orgId,
            "Prod",
            "sk_live_",
            "hash",
            "wxyz",
            PlatformApiScopes.DefaultDocumentScopes,
            createdByUserId: Guid.CreateVersion7());

        var repo = Substitute.For<IOneRepository>();
        repo.ListApiCredentialsAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ApiCredential>>(new List<ApiCredential> { key }));

        var handler = new ListApiCredentialsQueryHandler(repo);
        var list = (await handler.Handle(new ListApiCredentialsQuery(orgId), CancellationToken.None)).ToList();

        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(key.Id.ToString()));
        Assert.That(list[0].Name, Is.EqualTo("Prod"));
        Assert.That(list[0].Prefix, Is.EqualTo("sk_live_"));
        Assert.That(list[0].Hint, Is.EqualTo("wxyz"));
        Assert.That(list[0].Is_active, Is.True);
        Assert.That(list[0].Scopes, Is.EquivalentTo(new[]
        {
            PlatformApiScopes.LhdnDocumentsWrite,
            PlatformApiScopes.LhdnDocumentsRead
        }));
    }

    [Test]
    public async Task RevokeApiCredential_Marks_Inactive_And_Publishes_Event()
    {
        var orgId = Guid.CreateVersion7();
        var credential = new ApiCredential(
            orgId,
            "ToRevoke",
            "sk_test_",
            "hash-to-evict",
            "ab12",
            PlatformApiScopes.DefaultDocumentScopes);

        var repo = Substitute.For<IOneRepository>();
        repo.GetApiCredentialAsync(credential.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ApiCredential?>(credential));

        var bus = Substitute.For<IEventBus>();
        var handler = new RevokeApiCredentialCommandHandler(repo, bus);
        await handler.Handle(new RevokeApiCredentialCommand(orgId, credential.Id), CancellationToken.None);

        Assert.That(credential.IsActive, Is.False);
        await bus.Received(1).PublishAsync(Arg.Is<ApiKeyRevokedIntegrationEvent>(e =>
            e.OrganizationId == orgId && e.KeyHash == "hash-to-evict"));
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void TryGetApiKey_Accepts_Bearer_And_Raw_Prefix()
    {
        var bearerCtx = new DefaultHttpContext();
        bearerCtx.Request.Headers.Authorization = "Bearer sk_live_abc";
        Assert.That(ApiKeyAuthenticationMiddleware.TryGetApiKey(bearerCtx.Request, out var k1), Is.True);
        Assert.That(k1, Is.EqualTo("sk_live_abc"));

        var rawCtx = new DefaultHttpContext();
        rawCtx.Request.Headers.Authorization = "sk_test_xyz";
        Assert.That(ApiKeyAuthenticationMiddleware.TryGetApiKey(rawCtx.Request, out var k2), Is.True);
        Assert.That(k2, Is.EqualTo("sk_test_xyz"));

        var jwtCtx = new DefaultHttpContext();
        jwtCtx.Request.Headers.Authorization = "Bearer eyJhbGciOiJIUzI1NiJ9.e30.sig";
        Assert.That(ApiKeyAuthenticationMiddleware.TryGetApiKey(jwtCtx.Request, out _), Is.False);
    }

    [Test]
    public async Task GenerateApiCredential_With_Payments_Scopes_Only_Persists_Those_Scopes()
    {
        var repo = Substitute.For<IOneRepository>();
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("payonlysecretabcdefghij1234567890ab", "hash-of-random"));
        tokens.HashToken(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");

        ApiCredential? saved = null;
        repo.When(r => r.AddApiCredential(Arg.Any<ApiCredential>()))
            .Do(ci => saved = ci.Arg<ApiCredential>());

        var handler = new GenerateApiCredentialCommandHandler(repo, tokens);
        var orgId = Guid.CreateVersion7();
        var requested = new[]
        {
            PlatformApiScopes.PaymentsCheckoutsWrite,
            PlatformApiScopes.PaymentsCheckoutsRead
        };

        var result = await handler.Handle(
            new GenerateApiCredentialCommand(
                orgId,
                "Aura integrator",
                IsTestMode: true,
                CreatedByUserId: null,
                Scopes: requested),
            CancellationToken.None);

        Assert.That(result.Scopes, Is.EqualTo(PlatformApiScopes.DefaultAuraIntegratorScopes));
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Scopes, Is.EqualTo(PlatformApiScopes.DefaultAuraIntegratorScopes));
        Assert.That(PlatformApiScopes.Split(saved.Scopes), Is.EquivalentTo(requested));
        Assert.That(saved.Scopes, Does.Not.Contain("lhdn."));
    }

    [Test]
    public void GenerateApiCredential_Unknown_Scope_Throws()
    {
        var repo = Substitute.For<IOneRepository>();
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("abcdefghij1234567890abcdefghij1234567890", "hash"));
        tokens.HashToken(Arg.Any<string>()).Returns("hash");

        var handler = new GenerateApiCredentialCommandHandler(repo, tokens);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(
                new GenerateApiCredentialCommand(
                    Guid.CreateVersion7(),
                    "Bad",
                    IsTestMode: true,
                    Scopes: ["payments.checkouts:write", "not.a.real:scope"]),
                CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("Unknown API scope").And.Contain("not.a.real:scope"));
        repo.DidNotReceive().AddApiCredential(Arg.Any<ApiCredential>());
    }

    [Test]
    public void GenerateApiCredential_Empty_Scopes_Array_Throws()
    {
        var repo = Substitute.For<IOneRepository>();
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("abcdefghij1234567890abcdefghij1234567890", "hash"));
        tokens.HashToken(Arg.Any<string>()).Returns("hash");

        var handler = new GenerateApiCredentialCommandHandler(repo, tokens);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(
                new GenerateApiCredentialCommand(
                    Guid.CreateVersion7(),
                    "Empty",
                    IsTestMode: true,
                    Scopes: Array.Empty<string>()),
                CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("At least one scope"));
    }

    [Test]
    public async Task GenerateApiCredential_Omit_Scopes_Uses_Lhdn_Document_Default()
    {
        var repo = Substitute.For<IOneRepository>();
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("defaultscopesabcdefghij1234567890ab", "hash"));
        tokens.HashToken(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");

        ApiCredential? saved = null;
        repo.When(r => r.AddApiCredential(Arg.Any<ApiCredential>()))
            .Do(ci => saved = ci.Arg<ApiCredential>());

        var handler = new GenerateApiCredentialCommandHandler(repo, tokens);
        var result = await handler.Handle(
            new GenerateApiCredentialCommand(Guid.CreateVersion7(), "Compat", IsTestMode: true, Scopes: null),
            CancellationToken.None);

        Assert.That(result.Scopes, Is.EqualTo(PlatformApiScopes.DefaultDocumentScopes));
        Assert.That(saved!.Scopes, Is.EqualTo(PlatformApiScopes.DefaultDocumentScopes));
    }

    [Test]
    public void NormalizeAndValidate_Rejects_Unknown_And_Accepts_Catalog()
    {
        Assert.That(
            PlatformApiScopes.NormalizeAndValidate(null),
            Is.EqualTo(PlatformApiScopes.DefaultDocumentScopes));

        Assert.That(
            PlatformApiScopes.NormalizeAndValidate(
            [
                PlatformApiScopes.PaymentsCheckoutsWrite,
                PlatformApiScopes.PaymentsCheckoutsWrite, // dedupe
                PlatformApiScopes.PaymentsCheckoutsRead
            ]),
            Is.EqualTo(PlatformApiScopes.DefaultAuraIntegratorScopes));

        Assert.That(
            () => PlatformApiScopes.NormalizeAndValidate(["evil.admin:*"]),
            Throws.TypeOf<InvalidOperationException>());
    }
}
