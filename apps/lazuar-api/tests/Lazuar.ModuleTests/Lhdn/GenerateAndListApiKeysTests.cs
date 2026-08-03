using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Queries;
using Modules.Lhdn.Domain;
using Modules.Lhdn.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class GenerateAndListApiKeysTests
{
    [Test]
    public async Task GenerateApiKey_Returns_Rich_Result_With_Hint_And_Scopes()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("abcdefghij1234567890abcdefghij1234567890", "hash-of-random"));
        tokens.HashToken(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");

        DeveloperApiKey? saved = null;
        repo.When(r => r.AddDeveloperApiKey(Arg.Any<DeveloperApiKey>()))
            .Do(ci => saved = ci.Arg<DeveloperApiKey>());

        var handler = new GenerateApiKeyCommandHandler(repo, tokens);
        var orgId = Guid.CreateVersion7();
        var result = await handler.Handle(new GenerateApiKeyCommand(orgId, "Integration", IsTestMode: true), CancellationToken.None);

        Assert.That(result.Name, Is.EqualTo("Integration"));
        Assert.That(result.Prefix, Is.EqualTo("sk_test_"));
        Assert.That(result.PlainKey, Does.StartWith("sk_test_"));
        Assert.That(result.Hint, Is.EqualTo(result.PlainKey[^4..]));
        Assert.That(result.Scopes, Is.EqualTo(ApiKeyScopes.DefaultDocumentScopes));
        Assert.That(result.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.KeyHint, Is.EqualTo(result.Hint));
        Assert.That(saved.Scopes, Is.EqualTo(ApiKeyScopes.DefaultDocumentScopes));
        Assert.That(saved.OrganizationId, Is.EqualTo(orgId));
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListApiKeys_Returns_Metadata_Without_Secret()
    {
        var orgId = Guid.CreateVersion7();
        var key = new DeveloperApiKey(
            orgId,
            "Prod",
            "sk_live_",
            "hash",
            "wxyz",
            ApiKeyScopes.DefaultDocumentScopes);

        var repo = Substitute.For<ILhdnRepository>();
        repo.ListDeveloperApiKeysAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DeveloperApiKey>>(new List<DeveloperApiKey> { key }));

        var handler = new ListApiKeysQueryHandler(repo);
        var list = (await handler.Handle(new ListApiKeysQuery(orgId), CancellationToken.None)).ToList();

        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(key.Id.ToString()));
        Assert.That(list[0].Name, Is.EqualTo("Prod"));
        Assert.That(list[0].Prefix, Is.EqualTo("sk_live_"));
        Assert.That(list[0].Hint, Is.EqualTo("wxyz"));
        Assert.That(list[0].Is_active, Is.True);
        Assert.That(list[0].Scopes, Is.EquivalentTo(new[]
        {
            ApiKeyScopes.LhdnDocumentsWrite,
            ApiKeyScopes.LhdnDocumentsRead
        }));
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
}
