using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lazuar.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Queries;
using Modules.Lhdn.Domain;
using Modules.One.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

/// <summary>
/// Façade tests: Lhdn api-key commands/queries delegate to <see cref="IApiCredentialService"/>.
/// Core generate/list coverage lives in One.GenerateAndListApiCredentialsTests.
/// </summary>
[TestFixture]
public class GenerateAndListApiKeysTests
{
#pragma warning disable CS0618 // Obsolete Lhdn façades under test
    [Test]
    public async Task GenerateApiKey_Delegates_To_Platform_Service()
    {
        var service = Substitute.For<IApiCredentialService>();
        var orgId = Guid.CreateVersion7();
        var expected = new ApiCredentialGenerateResult(
            Guid.CreateVersion7(),
            "Integration",
            "sk_test_",
            "7890",
            DateTime.UtcNow,
            "sk_test_abcdefghij1234567890abcdefghij1234567890",
            ApiKeyScopes.DefaultDocumentScopes);

        service.GenerateAsync(
                orgId,
                "Integration",
                true,
                createdByUserId: null,
                scopes: null,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        var handler = new GenerateApiKeyCommandHandler(service);
        var result = await handler.Handle(new GenerateApiKeyCommand(orgId, "Integration", IsTestMode: true), CancellationToken.None);

        Assert.That(result.Id, Is.EqualTo(expected.Id));
        Assert.That(result.PlainKey, Is.EqualTo(expected.PlainKey));
        Assert.That(result.Hint, Is.EqualTo(expected.Hint));
        Assert.That(result.Scopes, Is.EqualTo(ApiKeyScopes.DefaultDocumentScopes));
        await service.Received(1).GenerateAsync(
            orgId,
            "Integration",
            true,
            createdByUserId: null,
            scopes: null,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListApiKeys_Delegates_To_Platform_Service()
    {
        var orgId = Guid.CreateVersion7();
        var snapshot = new ApiCredentialSnapshot(
            Guid.CreateVersion7(),
            "Prod",
            "sk_live_",
            "wxyz",
            IsActive: true,
            DateTime.UtcNow,
            ApiKeyScopes.DefaultDocumentScopes);

        var service = Substitute.For<IApiCredentialService>();
        service.ListAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ApiCredentialSnapshot>>(new List<ApiCredentialSnapshot> { snapshot }));

        var handler = new ListApiKeysQueryHandler(service);
        var list = (await handler.Handle(new ListApiKeysQuery(orgId), CancellationToken.None)).ToList();

        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].Id, Is.EqualTo(snapshot.Id.ToString()));
        Assert.That(list[0].Name, Is.EqualTo("Prod"));
        Assert.That(list[0].Hint, Is.EqualTo("wxyz"));
        Assert.That(list[0].Scopes, Is.EquivalentTo(new[]
        {
            ApiKeyScopes.LhdnDocumentsWrite,
            ApiKeyScopes.LhdnDocumentsRead
        }));
    }
#pragma warning restore CS0618

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
