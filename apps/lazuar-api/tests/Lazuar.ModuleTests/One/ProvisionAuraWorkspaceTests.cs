using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Contracts;
using Modules.One.Domain;
using Modules.One.Infrastructure.Configuration;
using Modules.One.Infrastructure.Services;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class ProvisionAuraWorkspaceTests
{
    private static ProvisionAuraWorkspaceCommandHandler CreateHandler(
        IOneRepository repo,
        out List<Organization> orgs,
        out List<ApiCredential> credentials,
        out List<TenantAppEntitlement> entitlements)
    {
        orgs = new List<Organization>();
        credentials = new List<ApiCredential>();
        entitlements = new List<TenantAppEntitlement>();

        var orgList = orgs;
        var credList = credentials;
        var entList = entitlements;

        repo.When(r => r.AddOrganization(Arg.Any<Organization>()))
            .Do(ci => orgList.Add(ci.Arg<Organization>()));
        repo.When(r => r.AddApiCredential(Arg.Any<ApiCredential>()))
            .Do(ci => credList.Add(ci.Arg<ApiCredential>()));
        repo.When(r => r.AddEntitlement(Arg.Any<TenantAppEntitlement>()))
            .Do(ci => entList.Add(ci.Arg<TenantAppEntitlement>()));

        repo.GetByExternalRefAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var product = ci.ArgAt<string>(0).Trim().ToLowerInvariant();
                var ext = ci.ArgAt<string>(1).Trim().ToLowerInvariant();
                return Task.FromResult(orgList.FirstOrDefault(o =>
                    string.Equals(o.ExternalProduct, product, StringComparison.Ordinal)
                    && string.Equals(o.ExternalOrgId, ext, StringComparison.Ordinal)));
            });

        repo.IsSlugUniqueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var slug = ci.ArgAt<string>(0);
                return Task.FromResult(orgList.All(o => o.Slug != slug));
            });

        repo.ListApiCredentialsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var orgId = ci.ArgAt<Guid>(0);
                IReadOnlyList<ApiCredential> list = credList.Where(c => c.OrganizationId == orgId).ToList();
                return Task.FromResult(list);
            });

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("abcdefghij1234567890abcdefghij1234567890", "hash-of-random"));
        tokens.HashToken(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");

        var eventBus = Substitute.For<IEventBus>();

        return new ProvisionAuraWorkspaceCommandHandler(repo, tokens, eventBus);
    }

    [Test]
    public async Task Provision_Create_Returns_Workspace_And_PlainKey_With_Aura_Scopes()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var orgs, out var credentials, out var entitlements);

        var auraOrgId = Guid.CreateVersion7();
        var result = await handler.Handle(
            new ProvisionAuraWorkspaceCommand(
                auraOrgId.ToString(),
                "Salon Melati",
                Slug: null,
                OwnerEmail: null,
                IsTestMode: true,
                KeyName: null,
                ActorUserId: null),
            CancellationToken.None);

        Assert.That(result.Created, Is.True);
        Assert.That(result.WorkspaceId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.AuraOrgId, Is.EqualTo(auraOrgId.ToString("D").ToLowerInvariant()));
        Assert.That(result.PlainKey, Does.StartWith("sk_test_"));
        Assert.That(result.Prefix, Is.EqualTo("sk_test_"));
        Assert.That(result.Hint, Is.EqualTo(result.PlainKey![^4..]));
        Assert.That(result.Scopes, Does.Contain(PlatformApiScopes.PaymentsCheckoutsWrite));
        Assert.That(result.Scopes, Does.Contain(PlatformApiScopes.PaymentsCheckoutsRead));
        Assert.That(result.Scopes, Does.Not.Contain(PlatformApiScopes.LhdnDocumentsWrite));
        Assert.That(result.Slug, Does.StartWith("aura-"));

        Assert.That(orgs, Has.Count.EqualTo(1));
        Assert.That(orgs[0].ExternalProduct, Is.EqualTo("aura"));
        Assert.That(orgs[0].ExternalOrgId, Is.EqualTo(result.AuraOrgId));
        Assert.That(orgs[0].Name, Is.EqualTo("Salon Melati"));

        Assert.That(entitlements, Has.Count.EqualTo(1));
        Assert.That(entitlements[0].AppId, Is.EqualTo("PAYMENTS"));

        Assert.That(credentials, Has.Count.EqualTo(1));
        Assert.That(credentials[0].Name, Is.EqualTo(ProvisionAuraWorkspaceCommandHandler.DefaultKeyName));
        Assert.That(credentials[0].Scopes, Is.EqualTo(PlatformApiScopes.DefaultAuraIntegratorScopes));

        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        repo.DidNotReceive().AddTenantMembership(Arg.Any<TenantMembership>());
    }

    [Test]
    public async Task Provision_Idempotent_Same_AuraOrgId_No_PlainKey_Same_Workspace()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _);

        var auraOrgId = Guid.CreateVersion7();
        var first = await handler.Handle(
            new ProvisionAuraWorkspaceCommand(
                auraOrgId.ToString("D"),
                "Salon Melati",
                null,
                null,
                IsTestMode: true,
                null,
                null),
            CancellationToken.None);

        var second = await handler.Handle(
            new ProvisionAuraWorkspaceCommand(
                auraOrgId.ToString("D").ToUpperInvariant(),
                "Different Name Ignored",
                null,
                null,
                IsTestMode: false,
                null,
                null),
            CancellationToken.None);

        Assert.That(first.Created, Is.True);
        Assert.That(first.PlainKey, Is.Not.Null.And.Not.Empty);
        Assert.That(second.Created, Is.False);
        Assert.That(second.PlainKey, Is.Null);
        Assert.That(second.WorkspaceId, Is.EqualTo(first.WorkspaceId));
        Assert.That(second.AuraOrgId, Is.EqualTo(first.AuraOrgId));
        Assert.That(second.ApiKeyId, Is.EqualTo(first.ApiKeyId));
        Assert.That(second.Prefix, Is.EqualTo(first.Prefix));
        Assert.That(second.Hint, Is.EqualTo(first.Hint));

        // Only one SaveChanges (create path); idempotent path does not re-mint.
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        repo.Received(1).AddOrganization(Arg.Any<Organization>());
        repo.Received(1).AddApiCredential(Arg.Any<ApiCredential>());
    }

    [Test]
    public void NormalizeAuraOrgId_Rejects_Invalid()
    {
        Assert.Throws<InvalidOperationException>(() => ProvisionAuraWorkspaceCommandHandler.NormalizeAuraOrgId(null));
        Assert.Throws<InvalidOperationException>(() => ProvisionAuraWorkspaceCommandHandler.NormalizeAuraOrgId(""));
        Assert.Throws<InvalidOperationException>(() => ProvisionAuraWorkspaceCommandHandler.NormalizeAuraOrgId("not-a-guid"));
    }

    [Test]
    public void BindExternalRef_Is_Idempotent_For_Same_Pair_Rejects_Conflict()
    {
        var org = new Organization("Test", "test-salon-1");
        org.BindExternalRef("aura", Guid.CreateVersion7().ToString("D"));
        var product = org.ExternalProduct!;
        var ext = org.ExternalOrgId!;
        org.BindExternalRef(product, ext); // ok

        Assert.Throws<InvalidOperationException>(() =>
            org.BindExternalRef("aura", Guid.CreateVersion7().ToString("D")));
    }

    [Test]
    public void ProvisionAuth_Accepts_Valid_Header_Secret()
    {
        var settings = new IntegratorProvisionSettings { Secret = "super-secret-provision-key-32chars!!" };
        var http = new DefaultHttpContext();
        http.Request.Headers[IntegratorProvisionAuth.ProvisionKeyHeader] = settings.Secret;

        var result = IntegratorProvisionAuth.Evaluate(http, settings);
        Assert.That(result.IsAuthorized, Is.True);
        Assert.That(result.IsSuperAdmin, Is.False);
    }

    [Test]
    public void ProvisionAuth_Rejects_Missing_Credentials()
    {
        var settings = new IntegratorProvisionSettings { Secret = "super-secret-provision-key-32chars!!" };
        var http = new DefaultHttpContext();

        var result = IntegratorProvisionAuth.Evaluate(http, settings);
        Assert.That(result.IsAuthorized, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public void ProvisionAuth_Rejects_Wrong_Secret()
    {
        var settings = new IntegratorProvisionSettings { Secret = "super-secret-provision-key-32chars!!" };
        var http = new DefaultHttpContext();
        http.Request.Headers[IntegratorProvisionAuth.ProvisionKeyHeader] = "wrong-secret";

        var result = IntegratorProvisionAuth.Evaluate(http, settings);
        Assert.That(result.IsAuthorized, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public void ProvisionAuth_Rejects_Client_Jwt_With_403()
    {
        var settings = new IntegratorProvisionSettings { Secret = "super-secret-provision-key-32chars!!" };
        var http = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString()),
                new Claim(ClaimTypes.Role, "CLIENT"),
                new Claim("is_system_admin", "false")
            },
            authenticationType: "Jwt");
        http.User = new ClaimsPrincipal(identity);

        var result = IntegratorProvisionAuth.Evaluate(http, settings);
        Assert.That(result.IsAuthorized, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public void ProvisionAuth_Accepts_SuperAdmin_Jwt()
    {
        var settings = new IntegratorProvisionSettings { Secret = "super-secret-provision-key-32chars!!" };
        var userId = Guid.CreateVersion7();
        var http = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "SUPER_ADMIN"),
                new Claim("is_system_admin", "true")
            },
            authenticationType: "Jwt");
        http.User = new ClaimsPrincipal(identity);

        var result = IntegratorProvisionAuth.Evaluate(http, settings);
        Assert.That(result.IsAuthorized, Is.True);
        Assert.That(result.IsSuperAdmin, Is.True);
        Assert.That(result.ActorUserId, Is.EqualTo(userId));
    }

    [Test]
    public void TenantSecurityMiddleware_Exempts_Provision_Path()
    {
        Assert.That(
            TenantSecurityMiddleware.IsTenantExemptPath(
                new PathString("/api/v1/one/integrations/workspaces/provision")),
            Is.True);
    }

    [Test]
    public async Task RateLimiter_Blocks_After_Budget()
    {
        var limiter = new IntegratorProvisionRateLimiter();
        Assert.That(await limiter.TryAcquireAsync("test-key", 2), Is.True);
        Assert.That(await limiter.TryAcquireAsync("test-key", 2), Is.True);
        Assert.That(await limiter.TryAcquireAsync("test-key", 2), Is.False);
    }
}
