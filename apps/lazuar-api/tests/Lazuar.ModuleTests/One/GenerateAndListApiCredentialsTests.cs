using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Application.Queries;
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
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
