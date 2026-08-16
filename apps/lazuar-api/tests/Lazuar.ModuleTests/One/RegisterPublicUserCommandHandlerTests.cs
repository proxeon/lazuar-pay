using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Contracts;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class RegisterPublicUserCommandHandlerTests
{
    private static readonly string[] CoreModules = ["OPS", "BILLING", "PAYMENTS", "CRM", "LHDN"];

    private static RegisterPublicUserCommandHandler CreateHandler(
        IOneRepository repo,
        out List<GlobalUser> users,
        out List<Organization> orgs,
        out List<TenantMembership> memberships,
        out List<TenantAppEntitlement> entitlements,
        out List<AppEntitlementGrantedIntegrationEvent> published,
        out IPasswordService passwords,
        out IEventBus eventBus)
    {
        users = [];
        orgs = [];
        memberships = [];
        entitlements = [];
        published = [];

        var userList = users;
        var orgList = orgs;
        var membershipList = memberships;
        var entitlementList = entitlements;

        repo.When(r => r.AddGlobalUser(Arg.Any<GlobalUser>()))
            .Do(ci => userList.Add(ci.Arg<GlobalUser>()));
        repo.When(r => r.AddOrganization(Arg.Any<Organization>()))
            .Do(ci => orgList.Add(ci.Arg<Organization>()));
        repo.When(r => r.AddTenantMembership(Arg.Any<TenantMembership>()))
            .Do(ci => membershipList.Add(ci.Arg<TenantMembership>()));
        repo.When(r => r.AddEntitlement(Arg.Any<TenantAppEntitlement>()))
            .Do(ci => entitlementList.Add(ci.Arg<TenantAppEntitlement>()));

        repo.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var email = ci.ArgAt<string>(0);
                return Task.FromResult(userList.FirstOrDefault(u =>
                    string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
            });

        repo.IsSlugUniqueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var slug = ci.ArgAt<string>(0);
                return Task.FromResult(orgList.All(o => o.Slug != slug));
            });

        passwords = Substitute.For<IPasswordService>();
        passwords.Hash(Arg.Any<string>()).Returns(ci => $"hash:{ci.Arg<string>()}");

        eventBus = Substitute.For<IEventBus>();
        var publishedList = published;
        eventBus.When(b => b.PublishAsync(Arg.Any<AppEntitlementGrantedIntegrationEvent>()))
            .Do(ci => publishedList.Add(ci.Arg<AppEntitlementGrantedIntegrationEvent>()));

        return new RegisterPublicUserCommandHandler(repo, passwords, eventBus);
    }

    [Test]
    public async Task HappyPath_Creates_User_Workspace_Admin_And_Core_Entitlements()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var users, out var orgs, out var memberships, out var entitlements, out var published, out _, out var eventBus);

        var userId = await handler.Handle(
            new RegisterPublicUserCommand("Jane@Example.com", "secret", "Jane", "Acme Corp", "Acme-Corp"),
            CancellationToken.None);

        Assert.That(users, Has.Count.EqualTo(1));
        Assert.That(users[0].Id, Is.EqualTo(userId));
        Assert.That(users[0].Email, Is.EqualTo("jane@example.com"));
        Assert.That(users[0].Name, Is.EqualTo("Jane"));
        Assert.That(users[0].IsSystemAdmin, Is.False);
        Assert.That(users[0].IsEmailVerified, Is.False);
        Assert.That(users[0].PasswordHash, Is.EqualTo("hash:secret"));

        Assert.That(orgs, Has.Count.EqualTo(1));
        Assert.That(orgs[0].Name, Is.EqualTo("Acme Corp"));
        Assert.That(orgs[0].Slug, Is.EqualTo("acme-corp"));
        Assert.That(orgs[0].IsActive, Is.True);

        Assert.That(memberships, Has.Count.EqualTo(1));
        Assert.That(memberships[0].GlobalUserId, Is.EqualTo(userId));
        Assert.That(memberships[0].OrganizationId, Is.EqualTo(orgs[0].Id));
        Assert.That(memberships[0].Role, Is.EqualTo("ADMIN"));

        Assert.That(entitlements.Select(e => e.AppId), Is.EquivalentTo(CoreModules));
        Assert.That(entitlements.Select(e => e.AppId), Does.Not.Contain("COMMERCE"));
        Assert.That(published.Select(e => e.AppId), Is.EquivalentTo(CoreModules));
        Assert.That(published.Select(e => e.TenantId).Distinct().Single(), Is.EqualTo(orgs[0].Id));

        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await eventBus.Received(5).PublishAsync(Arg.Any<AppEntitlementGrantedIntegrationEvent>());
    }

    [Test]
    public async Task Empty_Name_Falls_Back_To_Email_Local_Part()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var users, out _, out _, out _, out _, out _, out _);

        await handler.Handle(
            new RegisterPublicUserCommand("jane.doe@example.com", "secret", "  ", "Acme", "acme"),
            CancellationToken.None);

        Assert.That(users[0].Name, Is.EqualTo("jane.doe"));
    }

    [Test]
    public void Duplicate_Email_Any_Case_Throws_And_Writes_Nothing()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var users, out var orgs, out _, out _, out _, out _, out _);
        users.Add(new GlobalUser("taken@example.com", "Existing", "hash"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RegisterPublicUserCommand("Taken@Example.com", "secret", "New", "Other", "other-co"),
            CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("already exists"));
        Assert.That(orgs, Is.Empty);
        repo.DidNotReceive().AddOrganization(Arg.Any<Organization>());
        repo.DidNotReceive().AddGlobalUser(Arg.Any<GlobalUser>());
        repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void Taken_Slug_Throws_And_Writes_Nothing()
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var users, out var orgs, out _, out _, out _, out _, out _);
        orgs.Add(new Organization("Existing", "acme-corp"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RegisterPublicUserCommand("new@example.com", "secret", "New", "Acme", "acme-corp"),
            CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("already taken"));
        Assert.That(users, Is.Empty);
        repo.DidNotReceive().AddGlobalUser(Arg.Any<GlobalUser>());
        repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [TestCase("admin")]
    [TestCase("portal")]
    [TestCase("system")]
    [TestCase("billplz")]
    public void Reserved_Slug_Throws_Business_Rule_And_Writes_Nothing(string slug)
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var users, out var orgs, out _, out _, out _, out _, out _);

        Assert.ThrowsAsync<BusinessRuleValidationException>(() => handler.Handle(
            new RegisterPublicUserCommand("new@example.com", "secret", "New", "Acme", slug),
            CancellationToken.None));

        Assert.That(users, Is.Empty);
        Assert.That(orgs, Is.Empty);
        repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [TestCase("ab")]
    [TestCase("acme--corp")]
    [TestCase("-acme")]
    public void Malformed_Slug_Throws_Business_Rule_And_Writes_Nothing(string slug)
    {
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var users, out var orgs, out _, out _, out _, out _, out _);

        Assert.ThrowsAsync<BusinessRuleValidationException>(() => handler.Handle(
            new RegisterPublicUserCommand("new@example.com", "secret", "New", "Acme", slug),
            CancellationToken.None));

        Assert.That(users, Is.Empty);
        Assert.That(orgs, Is.Empty);
        repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void Handler_And_OneDbContext_Have_No_AppAccessRequest()
    {
        var oneTypes = typeof(RegisterPublicUserCommandHandler).Assembly.GetTypes()
            .Concat(typeof(Organization).Assembly.GetTypes())
            .Concat(typeof(OneDbContext).Assembly.GetTypes());

        Assert.That(oneTypes.Any(t => t.Name.Contains("AppAccessRequest", StringComparison.Ordinal)), Is.False);

        Assert.That(
            typeof(OneDbContext).GetProperties()
                .Any(p => p.Name.Contains("AppAccessRequest", StringComparison.Ordinal)
                          || p.PropertyType.Name.Contains("AppAccessRequest", StringComparison.Ordinal)),
            Is.False);
    }
}
