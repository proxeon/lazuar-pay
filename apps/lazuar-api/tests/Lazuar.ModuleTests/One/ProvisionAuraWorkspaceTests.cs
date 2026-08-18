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
using Modules.One.Infrastructure;
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
        out List<TenantAppEntitlement> entitlements,
        out List<TenantWebhookEndpoint> webhooks,
        out List<TenantMembership> memberships,
        out List<GlobalUser> users)
    {
        orgs = new List<Organization>();
        credentials = new List<ApiCredential>();
        entitlements = new List<TenantAppEntitlement>();
        webhooks = new List<TenantWebhookEndpoint>();
        memberships = new List<TenantMembership>();
        users = new List<GlobalUser>();

        var orgList = orgs;
        var credList = credentials;
        var entList = entitlements;
        var webhookList = webhooks;
        var membershipList = memberships;
        var userList = users;

        repo.When(r => r.AddOrganization(Arg.Any<Organization>()))
            .Do(ci => orgList.Add(ci.Arg<Organization>()));
        repo.When(r => r.AddApiCredential(Arg.Any<ApiCredential>()))
            .Do(ci => credList.Add(ci.Arg<ApiCredential>()));
        repo.When(r => r.AddEntitlement(Arg.Any<TenantAppEntitlement>()))
            .Do(ci => entList.Add(ci.Arg<TenantAppEntitlement>()));
        repo.When(r => r.AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>()))
            .Do(ci => webhookList.Add(ci.Arg<TenantWebhookEndpoint>()));
        repo.When(r => r.AddTenantMembership(Arg.Any<TenantMembership>()))
            .Do(ci => membershipList.Add(ci.Arg<TenantMembership>()));

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

        repo.ListWebhookEndpointsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var orgId = ci.ArgAt<Guid>(0);
                IReadOnlyList<TenantWebhookEndpoint> list = webhookList
                    .Where(w => w.OrganizationId == orgId)
                    .ToList();
                return Task.FromResult(list);
            });

        repo.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var email = ci.ArgAt<string>(0).Trim().ToLowerInvariant();
                return Task.FromResult(userList.FirstOrDefault(u =>
                    string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
            });

        repo.GetMembershipAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var userId = ci.ArgAt<Guid>(0);
                var orgId = ci.ArgAt<Guid>(1);
                return Task.FromResult(membershipList.FirstOrDefault(m =>
                    m.GlobalUserId == userId && m.OrganizationId == orgId));
            });

        repo.HasMembershipAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var userId = ci.ArgAt<Guid>(0);
                var orgId = ci.ArgAt<Guid>(1);
                return Task.FromResult(membershipList.Any(m =>
                    m.GlobalUserId == userId && m.OrganizationId == orgId));
            });

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(40).Returns(new GeneratedToken("abcdefghij1234567890abcdefghij1234567890", "hash-of-random"));
        tokens.GenerateSecureToken(24).Returns(new GeneratedToken("webhooksecrettoken24ch", "hash-webhook"));
        tokens.HashToken(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");

        var eventBus = Substitute.For<IEventBus>();
        var vault = Substitute.For<ISecretVault>();
        vault.Encrypt(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));
        vault.Decrypt(Arg.Any<string>()).Returns(_ =>
            throw new System.Security.Cryptography.CryptographicException());

        return new ProvisionAuraWorkspaceCommandHandler(repo, tokens, eventBus, vault);
    }

    private static ProvisionAuraWorkspaceCommand Cmd(
        string auraOrgId,
        string displayName = "Salon Melati",
        string? slug = null,
        string? ownerEmail = null,
        string? ownerRole = null,
        bool isTestMode = true,
        string? keyName = null,
        string? webhookUrl = null,
        IReadOnlyList<string>? webhookEvents = null,
        Guid? actorUserId = null,
        string? externalProduct = null) =>
        new(
            auraOrgId,
            displayName,
            slug,
            ownerEmail,
            ownerRole,
            isTestMode,
            keyName,
            webhookUrl,
            webhookEvents,
            actorUserId,
            externalProduct);

    [Test]
    public async Task Provision_Create_Returns_Workspace_And_PlainKey_With_Aura_Scopes()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var orgs, out var credentials, out var entitlements, out _, out _, out _);

        var auraOrgId = Guid.CreateVersion7();
        var result = await handler.Handle(Cmd(auraOrgId.ToString()), CancellationToken.None);

        Assert.That(result.Created, Is.True);
        Assert.That(result.WorkspaceId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.AuraOrgId, Is.EqualTo(auraOrgId.ToString("D").ToLowerInvariant()));
        Assert.That(result.PlainKey, Does.StartWith("sk_test_"));
        Assert.That(result.Prefix, Is.EqualTo("sk_test_"));
        Assert.That(result.Hint, Is.EqualTo(result.PlainKey![^4..]));
        Assert.That(result.Scopes, Does.Contain(PlatformApiScopes.PaymentsCheckoutsWrite));
        Assert.That(result.Scopes, Does.Contain(PlatformApiScopes.PaymentsCheckoutsRead));
        Assert.That(result.Scopes, Does.Contain(PlatformApiScopes.WebhooksEndpointsManage));
        Assert.That(result.Scopes, Does.Not.Contain(PlatformApiScopes.LhdnDocumentsWrite));
        Assert.That(result.Slug, Does.StartWith("aura-"));
        Assert.That(result.ExternalProduct, Is.EqualTo("aura"));
        Assert.That(result.ExternalOrgId, Is.EqualTo(result.AuraOrgId));
        Assert.That(result.WebhookEndpointId, Is.Null);
        Assert.That(result.WebhookSecretKey, Is.Null);
        Assert.That(result.OwnerAttached, Is.False);
        Assert.That(result.OwnerStatus, Is.EqualTo(ProvisionAuraWorkspaceCommandHandler.OwnerStatusNotRequested));

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
        repo.DidNotReceive().AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>());
    }

    [Test]
    public async Task Provision_Default_Scopes_Include_Webhooks_Manage()
    {
        var scopes = PlatformApiScopes.Split(PlatformApiScopes.DefaultAuraIntegratorScopes);
        Assert.That(scopes, Does.Contain(PlatformApiScopes.WebhooksEndpointsManage));
        Assert.That(scopes, Does.Contain(PlatformApiScopes.PaymentsCheckoutsWrite));
        Assert.That(scopes, Does.Contain(PlatformApiScopes.PaymentsCheckoutsRead));
    }

    [Test]
    public async Task Provision_Create_With_WebhookUrl_Returns_Secret_Once()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out var webhooks, out _, out _);

        var auraOrgId = Guid.CreateVersion7();
        const string url = "https://aura.example/hooks/hub";
        var result = await handler.Handle(Cmd(auraOrgId.ToString(), webhookUrl: url), CancellationToken.None);

        Assert.That(result.Created, Is.True);
        Assert.That(result.WebhookEndpointId, Is.Not.Null);
        Assert.That(result.WebhookUrl, Is.EqualTo(url));
        Assert.That(result.WebhookIsActive, Is.True);
        Assert.That(result.WebhookSecretKey, Does.StartWith("whsec_"));
        Assert.That(result.WebhookSecretHint, Is.EqualTo(result.WebhookSecretKey![^4..]));
        Assert.That(result.WebhookEnabledEvents, Is.EquivalentTo(new[] { "payment.completed", "payment.failed" }));

        Assert.That(webhooks, Has.Count.EqualTo(1));
        Assert.That(webhooks[0].Url, Is.EqualTo(url));
        Assert.That(webhooks[0].EnabledEvents, Is.EquivalentTo(new[] { "payment.completed", "payment.failed" }));

        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        repo.Received(1).AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>());
    }

    [Test]
    public async Task Provision_Idempotent_With_WebhookUrl_No_Secret_Remint()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out var webhooks, out _, out _);

        var auraOrgId = Guid.CreateVersion7();
        const string url = "https://aura.example/hooks/hub";
        var first = await handler.Handle(Cmd(auraOrgId.ToString("D"), webhookUrl: url), CancellationToken.None);
        var second = await handler.Handle(Cmd(auraOrgId.ToString("D"), webhookUrl: url), CancellationToken.None);

        Assert.That(first.Created, Is.True);
        Assert.That(first.WebhookSecretKey, Is.Not.Null.And.Not.Empty);
        Assert.That(second.Created, Is.False);
        Assert.That(second.PlainKey, Is.Null);
        Assert.That(second.WebhookSecretKey, Is.Null);
        Assert.That(second.WebhookEndpointId, Is.EqualTo(first.WebhookEndpointId));
        Assert.That(second.WebhookUrl, Is.EqualTo(url));
        Assert.That(webhooks, Has.Count.EqualTo(1));

        // Create SaveChanges only once for org; idempotent path does not re-add webhook.
        repo.Received(1).AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Provision_Idempotent_Heal_Missing_Webhook()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out var webhooks, out _, out _);

        var auraOrgId = Guid.CreateVersion7();
        const string url = "https://aura.example/hooks/heal";
        var first = await handler.Handle(Cmd(auraOrgId.ToString("D")), CancellationToken.None);
        Assert.That(first.Created, Is.True);
        Assert.That(first.WebhookEndpointId, Is.Null);
        Assert.That(webhooks, Has.Count.EqualTo(0));

        var second = await handler.Handle(Cmd(auraOrgId.ToString("D"), webhookUrl: url), CancellationToken.None);
        Assert.That(second.Created, Is.False);
        Assert.That(second.PlainKey, Is.Null);
        Assert.That(second.WebhookEndpointId, Is.Not.Null);
        Assert.That(second.WebhookSecretKey, Does.StartWith("whsec_"));
        Assert.That(second.WebhookUrl, Is.EqualTo(url));
        Assert.That(webhooks, Has.Count.EqualTo(1));

        // First create + heal webhook save
        await repo.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        repo.Received(1).AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>());
    }

    [Test]
    public async Task Provision_Without_WebhookUrl_Omits_Webhook()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out _, out _, out _);

        var result = await handler.Handle(Cmd(Guid.CreateVersion7().ToString()), CancellationToken.None);

        Assert.That(result.WebhookEndpointId, Is.Null);
        Assert.That(result.WebhookSecretKey, Is.Null);
        repo.DidNotReceive().AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>());
    }

    [Test]
    public void Provision_Rejects_Invalid_WebhookUrl()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out _, out _, out _);
        var aura = Guid.CreateVersion7().ToString();

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Cmd(aura, webhookUrl: "not-a-url"), CancellationToken.None));
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Cmd(aura, webhookUrl: "/relative/path"), CancellationToken.None));
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Cmd(aura, webhookUrl: "http://evil.example/hook"), CancellationToken.None));
        // Whitespace-only is treated as omitted (optional field), not a validation error.
    }

    [Test]
    public async Task Provision_Allows_Http_Loopback_WebhookUrl()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out var webhooks, out _, out _);

        var result = await handler.Handle(
            Cmd(Guid.CreateVersion7().ToString(), webhookUrl: "http://localhost:3000/hooks"),
            CancellationToken.None);

        Assert.That(result.Created, Is.True);
        Assert.That(result.WebhookUrl, Is.EqualTo("http://localhost:3000/hooks"));
        Assert.That(webhooks, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Provision_Owner_Admin_When_User_Exists()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out _, out var memberships, out var users);

        var user = new GlobalUser("owner@example.com", "Owner", "hash");
        users.Add(user);

        var result = await handler.Handle(
            Cmd(Guid.CreateVersion7().ToString(), ownerEmail: "owner@example.com"),
            CancellationToken.None);

        Assert.That(result.OwnerAttached, Is.True);
        Assert.That(result.OwnerStatus, Is.EqualTo(ProvisionAuraWorkspaceCommandHandler.OwnerStatusAttached));
        Assert.That(result.OwnerRole, Is.EqualTo("ADMIN"));
        Assert.That(memberships, Has.Count.EqualTo(1));
        Assert.That(memberships[0].Role, Is.EqualTo("ADMIN"));
        Assert.That(memberships[0].GlobalUserId, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task Provision_Owner_SuperAdmin_When_Requested()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out _, out var memberships, out var users);

        users.Add(new GlobalUser("owner@example.com", "Owner", "hash"));

        var result = await handler.Handle(
            Cmd(Guid.CreateVersion7().ToString(), ownerEmail: "owner@example.com", ownerRole: "SUPER_ADMIN"),
            CancellationToken.None);

        Assert.That(result.OwnerAttached, Is.True);
        Assert.That(result.OwnerRole, Is.EqualTo("SUPER_ADMIN"));
        Assert.That(memberships[0].Role, Is.EqualTo("SUPER_ADMIN"));
        // Never global system admin
        Assert.That(users[0].IsSystemAdmin, Is.False);
    }

    [Test]
    public async Task Provision_Owner_UserNotFound_Does_Not_Fail()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var orgs, out _, out _, out _, out var memberships, out _);

        var result = await handler.Handle(
            Cmd(Guid.CreateVersion7().ToString(), ownerEmail: "missing@example.com"),
            CancellationToken.None);

        Assert.That(result.Created, Is.True);
        Assert.That(result.OwnerAttached, Is.False);
        Assert.That(result.OwnerStatus, Is.EqualTo(ProvisionAuraWorkspaceCommandHandler.OwnerStatusUserNotFound));
        Assert.That(orgs, Has.Count.EqualTo(1));
        Assert.That(memberships, Has.Count.EqualTo(0));
        repo.DidNotReceive().AddTenantMembership(Arg.Any<TenantMembership>());
    }

    [Test]
    public void Provision_Owner_Invalid_Role_Rejected()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out _, out _, out _);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                Cmd(Guid.CreateVersion7().ToString(), ownerEmail: "a@b.com", ownerRole: "CLIENT"),
                CancellationToken.None));
    }

    [Test]
    public async Task Provision_Owner_Idempotent_Does_Not_Duplicate_Membership()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out _, out var memberships, out var users);

        users.Add(new GlobalUser("owner@example.com", "Owner", "hash"));
        var aura = Guid.CreateVersion7().ToString("D");

        var first = await handler.Handle(Cmd(aura, ownerEmail: "owner@example.com"), CancellationToken.None);
        var second = await handler.Handle(Cmd(aura, ownerEmail: "owner@example.com"), CancellationToken.None);

        Assert.That(first.OwnerAttached, Is.True);
        Assert.That(second.OwnerAttached, Is.True);
        Assert.That(second.Created, Is.False);
        Assert.That(memberships, Has.Count.EqualTo(1));
        repo.Received(1).AddTenantMembership(Arg.Any<TenantMembership>());
    }

    [Test]
    public async Task Provision_Idempotent_Same_AuraOrgId_No_PlainKey_Same_Workspace()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out _, out _, out _);

        var auraOrgId = Guid.CreateVersion7();
        var first = await handler.Handle(Cmd(auraOrgId.ToString("D")), CancellationToken.None);
        var second = await handler.Handle(
            Cmd(auraOrgId.ToString("D").ToUpperInvariant(), displayName: "Different Name Ignored", isTestMode: false),
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
    public async Task Provision_SecondProduct_NonGuidOrgId_Works()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var orgs, out var credentials, out _, out _, out _, out _);

        var result = await handler.Handle(
            Cmd("Tenant-001", displayName: "Demo Tenant", externalProduct: "demo-app"),
            CancellationToken.None);

        Assert.That(result.Created, Is.True);
        Assert.That(result.ExternalProduct, Is.EqualTo("demo-app"));
        Assert.That(result.ExternalOrgId, Is.EqualTo("tenant-001"));
        Assert.That(result.AuraOrgId, Is.EqualTo("tenant-001"));
        Assert.That(result.Slug, Does.StartWith("demo-app-"));
        Assert.That(result.PlainKey, Does.StartWith("sk_test_"));
        Assert.That(orgs[0].ExternalProduct, Is.EqualTo("demo-app"));
        Assert.That(orgs[0].ExternalOrgId, Is.EqualTo("tenant-001"));
        Assert.That(credentials[0].Name, Is.EqualTo("demo-app bootstrap"));
        Assert.That(credentials[0].Scopes, Is.EqualTo(PlatformApiScopes.DefaultAuraIntegratorScopes));
    }

    [Test]
    public async Task Provision_SecondProduct_Idempotent_On_Product_And_Org()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, out _, out _, out _);

        var first = await handler.Handle(
            Cmd("acme-9", displayName: "Acme", externalProduct: "demo-app"),
            CancellationToken.None);
        var second = await handler.Handle(
            Cmd("ACME-9", displayName: "Ignored", externalProduct: "demo-app"),
            CancellationToken.None);

        Assert.That(first.Created, Is.True);
        Assert.That(second.Created, Is.False);
        Assert.That(second.WorkspaceId, Is.EqualTo(first.WorkspaceId));
        Assert.That(second.PlainKey, Is.Null);
        Assert.That(second.ExternalProduct, Is.EqualTo("demo-app"));
    }

    [Test]
    public void NormalizeExternalProduct_Defaults_And_Validates()
    {
        Assert.That(ProvisionAuraWorkspaceCommandHandler.NormalizeExternalProduct(null), Is.EqualTo("aura"));
        Assert.That(ProvisionAuraWorkspaceCommandHandler.NormalizeExternalProduct("Demo-App"), Is.EqualTo("demo-app"));
        Assert.That(ProvisionAuraWorkspaceCommandHandler.NormalizeExternalProduct("aurabook"), Is.EqualTo("aura"));
        Assert.That(ProvisionAuraWorkspaceCommandHandler.NormalizeExternalProduct("AuraBook"), Is.EqualTo("aura"));
        Assert.Throws<InvalidOperationException>(() =>
            ProvisionAuraWorkspaceCommandHandler.NormalizeExternalProduct("1bad"));
        Assert.Throws<InvalidOperationException>(() =>
            ProvisionAuraWorkspaceCommandHandler.NormalizeExternalProduct("has space"));
    }

    [Test]
    public void NormalizeExternalOrgId_Aura_Requires_Guid_Other_Allows_String()
    {
        var guid = Guid.CreateVersion7().ToString("D");
        Assert.That(
            ProvisionAuraWorkspaceCommandHandler.NormalizeExternalOrgId(guid, "aura"),
            Is.EqualTo(guid.ToLowerInvariant()));
        Assert.Throws<InvalidOperationException>(() =>
            ProvisionAuraWorkspaceCommandHandler.NormalizeExternalOrgId("not-guid", "aura"));
        Assert.Throws<InvalidOperationException>(() =>
            ProvisionAuraWorkspaceCommandHandler.NormalizeExternalOrgId("not-guid", "aurabook"));
        Assert.That(
            ProvisionAuraWorkspaceCommandHandler.NormalizeExternalOrgId(guid, "aurabook"),
            Is.EqualTo(guid.ToLowerInvariant()));
        Assert.That(
            ProvisionAuraWorkspaceCommandHandler.NormalizeExternalOrgId("Tenant-X", "demo-app"),
            Is.EqualTo("tenant-x"));
    }

    [Test]
    public void ResolveProvisionIdentity_OnlyAuraOrgId_DefaultsProductAura()
    {
        var guid = Guid.CreateVersion7().ToString("D");
        var id = ProvisionAuraWorkspaceCommandHandler.ResolveProvisionIdentity(
            externalProduct: null,
            externalOrgId: null,
            auraOrgId: guid);
        Assert.That(id.Product, Is.EqualTo("aura"));
        Assert.That(id.ExternalOrgIdRaw, Is.EqualTo(guid));
    }

    [Test]
    public void ResolveProvisionIdentity_ExternalOrgIdWithoutProduct_ThrowsExternalProductRequired()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProvisionAuraWorkspaceCommandHandler.ResolveProvisionIdentity(
                externalProduct: null,
                externalOrgId: "tenant-001",
                auraOrgId: null));
        Assert.That(ex!.Message, Does.StartWith(
            ProvisionAuraWorkspaceCommandHandler.ErrorExternalProductRequired));
    }

    [Test]
    public void ResolveProvisionIdentity_ExternalOrgIdAndAuraOrgIdWithoutProduct_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProvisionAuraWorkspaceCommandHandler.ResolveProvisionIdentity(
                null, "tenant-001", Guid.CreateVersion7().ToString("D")));
    }

    [Test]
    public void ResolveProvisionIdentity_ExplicitProductPlusExternalOrgId_Canonical()
    {
        var id = ProvisionAuraWorkspaceCommandHandler.ResolveProvisionIdentity(
            "Demo-App", "Tenant-001", null);
        Assert.That(id.Product, Is.EqualTo("demo-app"));
        Assert.That(id.ExternalOrgIdRaw, Is.EqualTo("Tenant-001"));
    }

    [Test]
    public async Task Provision_Aurabook_Alias_Does_Not_Fork_Existing_Aura_Workspace()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var orgs, out _, out _, out _, out _, out _);

        var guid = Guid.CreateVersion7().ToString("D");
        var first = await handler.Handle(Cmd(guid, externalProduct: "aura"), CancellationToken.None);
        var second = await handler.Handle(Cmd(guid, displayName: "Ignored", externalProduct: "aurabook"), CancellationToken.None);

        Assert.That(first.Created, Is.True);
        Assert.That(first.ExternalProduct, Is.EqualTo("aura"));
        Assert.That(second.Created, Is.False);
        Assert.That(second.PlainKey, Is.Null);
        Assert.That(second.WorkspaceId, Is.EqualTo(first.WorkspaceId));
        Assert.That(second.ExternalProduct, Is.EqualTo("aura"));
        Assert.That(orgs, Has.Count.EqualTo(1));
        Assert.That(orgs[0].ExternalProduct, Is.EqualTo("aura"));
        repo.Received(1).AddOrganization(Arg.Any<Organization>());
    }

    [Test]
    public void DefaultKeyNameFor_Aura_Vs_OtherProduct()
    {
        Assert.That(ProvisionAuraWorkspaceCommandHandler.DefaultKeyNameFor("aura"), Is.EqualTo("Aura bootstrap"));
        Assert.That(ProvisionAuraWorkspaceCommandHandler.DefaultKeyNameFor("aurabook"), Is.EqualTo("Aura bootstrap"));
        Assert.That(ProvisionAuraWorkspaceCommandHandler.DefaultKeyNameFor("demo-app"), Is.EqualTo("demo-app bootstrap"));
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
    public void ProvisionAuth_Accepts_System_Admin_Jwt()
    {
        var settings = new IntegratorProvisionSettings { Secret = "super-secret-provision-key-32chars!!" };
        var userId = Guid.CreateVersion7();
        var http = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "CLIENT"),
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
    public void ProvisionAuth_Rejects_Membership_SuperAdmin_Without_System_Admin_Claim()
    {
        var settings = new IntegratorProvisionSettings { Secret = "super-secret-provision-key-32chars!!" };
        var http = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString()),
                new Claim(ClaimTypes.Role, "SUPER_ADMIN"),
                new Claim("is_system_admin", "false")
            },
            authenticationType: "Jwt");
        http.User = new ClaimsPrincipal(identity);

        var result = IntegratorProvisionAuth.Evaluate(http, settings);
        Assert.That(result.IsAuthorized, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
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

    [Test]
    public async Task Companion_Webhook_Auth_ApiClient_With_Scope_Same_Tenant_Allowed()
    {
        var workspaceId = Guid.CreateVersion7();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "api_client"),
                new Claim(ClaimTypes.Role, "API_CLIENT"),
                new Claim("scope", PlatformApiScopes.WebhooksEndpointsManage)
            },
            "ApiKey"));
        http.Items["TenantId"] = workspaceId;

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.IsSystemAdmin.Returns(false);
        ctx.TenantId.Returns(workspaceId);
        ctx.UserId.Returns(Guid.Empty);

        var query = Substitute.For<IOneQueryService>();
        var ok = await WebhookEndpoints.CanAccessWorkspaceWebhooksAsync(workspaceId, http, ctx, query, manageRequired: true);
        Assert.That(ok, Is.True);
    }

    [Test]
    public async Task Companion_Webhook_Auth_ApiClient_Without_Scope_Denied()
    {
        var workspaceId = Guid.CreateVersion7();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "api_client"),
                new Claim(ClaimTypes.Role, "API_CLIENT"),
                new Claim("scope", PlatformApiScopes.PaymentsCheckoutsWrite)
            },
            "ApiKey"));

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.IsSystemAdmin.Returns(false);
        ctx.TenantId.Returns(workspaceId);

        var query = Substitute.For<IOneQueryService>();
        var ok = await WebhookEndpoints.CanAccessWorkspaceWebhooksAsync(workspaceId, http, ctx, query, manageRequired: true);
        Assert.That(ok, Is.False);
    }

    [Test]
    public async Task Companion_Webhook_Auth_ApiClient_Idor_Cross_Tenant_Denied()
    {
        var keyOrg = Guid.CreateVersion7();
        var otherWorkspace = Guid.CreateVersion7();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "api_client"),
                new Claim(ClaimTypes.Role, "API_CLIENT"),
                new Claim("scope", PlatformApiScopes.WebhooksEndpointsManage)
            },
            "ApiKey"));

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.IsSystemAdmin.Returns(false);
        ctx.TenantId.Returns(keyOrg);

        var query = Substitute.For<IOneQueryService>();
        var ok = await WebhookEndpoints.CanAccessWorkspaceWebhooksAsync(otherWorkspace, http, ctx, query, manageRequired: true);
        Assert.That(ok, Is.False);
    }

    [Test]
    public async Task Companion_Webhook_Auth_OrgAdmin_Membership_Allowed()
    {
        var workspaceId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "ADMIN")
            },
            "Jwt"));

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.IsSystemAdmin.Returns(false);
        ctx.TenantId.Returns(workspaceId);
        ctx.UserId.Returns(userId);

        var query = Substitute.For<IOneQueryService>();
        query.GetTenantRoleAsync(userId, workspaceId).Returns("ADMIN");

        var ok = await WebhookEndpoints.CanAccessWorkspaceWebhooksAsync(workspaceId, http, ctx, query, manageRequired: true);
        Assert.That(ok, Is.True);
    }

    [Test]
    public void WebhookUrlValidator_Rejects_Http_NonLoopback()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WebhookUrlValidator.NormalizeAndValidate("http://example.com/hook"));
        Assert.That(
            WebhookUrlValidator.NormalizeAndValidate("https://example.com/hook"),
            Is.EqualTo("https://example.com/hook"));
        Assert.That(
            WebhookUrlValidator.NormalizeAndValidate("http://127.0.0.1:8080/x"),
            Is.EqualTo("http://127.0.0.1:8080/x"));
    }

    [Test]
    public async Task CreateWebhookEndpoint_Validates_Https()
    {
        var repo = Substitute.For<IOneRepository>();
        repo.ListWebhookEndpointsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantWebhookEndpoint>>([]));
        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(24).Returns(new GeneratedToken("abcdefghijklmnopqrstuvwx", "h"));
        var handler = new CreateWebhookEndpointCommandHandler(repo, tokens, IdentityVault());

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new CreateWebhookEndpointCommand(Guid.CreateVersion7(), "http://evil.test/hook"),
                CancellationToken.None));

        var ok = await handler.Handle(
            new CreateWebhookEndpointCommand(Guid.CreateVersion7(), "https://good.example/hook"),
            CancellationToken.None);
        Assert.That(ok.SecretKey, Does.StartWith("whsec_"));
        Assert.That(ok.Url, Is.EqualTo("https://good.example/hook"));
        repo.Received(1).AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>());
    }

    [Test]
    public async Task CreateWebhookEndpoint_SameUrl_IsIdempotent_DoesNotReRevealSecret()
    {
        var repo = Substitute.For<IOneRepository>();
        var endpoints = new List<TenantWebhookEndpoint>();
        repo.When(r => r.AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>()))
            .Do(ci => endpoints.Add(ci.Arg<TenantWebhookEndpoint>()));
        repo.ListWebhookEndpointsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult((IReadOnlyList<TenantWebhookEndpoint>)endpoints.ToList()));

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(24).Returns(new GeneratedToken("abcdefghijklmnopqrstuvwx", "h"));
        var handler = new CreateWebhookEndpointCommandHandler(repo, tokens, IdentityVault());
        var orgId = Guid.CreateVersion7();
        const string url = "https://good.example/hook";

        var first = await handler.Handle(new CreateWebhookEndpointCommand(orgId, url), CancellationToken.None);
        var second = await handler.Handle(new CreateWebhookEndpointCommand(orgId, url), CancellationToken.None);

        Assert.That(first.SecretKey, Does.StartWith("whsec_"));
        Assert.That(second.SecretKey, Is.Null.Or.Empty);
        Assert.That(second.Id, Is.EqualTo(first.Id));
        repo.Received(1).AddWebhookEndpoint(Arg.Any<TenantWebhookEndpoint>());
    }

    [Test]
    public async Task CreateWebhookEndpoint_Rejects_Http_NonLoopback()
    {
        var repo = Substitute.For<IOneRepository>();
        var tokens = Substitute.For<ITokenGeneratorService>();
        var handler = new CreateWebhookEndpointCommandHandler(repo, tokens, IdentityVault());

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(
                new CreateWebhookEndpointCommand(Guid.CreateVersion7(), "http://evil.test/hook"),
                CancellationToken.None));
    }

    private static ISecretVault IdentityVault()
    {
        var vault = Substitute.For<ISecretVault>();
        vault.Encrypt(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));
        vault.Decrypt(Arg.Any<string>()).Returns(_ =>
            throw new System.Security.Cryptography.CryptographicException());
        return vault;
    }
}
