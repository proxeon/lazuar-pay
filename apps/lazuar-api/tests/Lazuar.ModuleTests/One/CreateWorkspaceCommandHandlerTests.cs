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
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class CreateWorkspaceCommandHandlerTests
{
    private static readonly string[] CoreApps = ["OPS", "BILLING", "PAYMENTS", "CRM", "LHDN"];

    private static CreateWorkspaceCommandHandler CreateHandler(
        IOneRepository repo,
        out List<Organization> orgs,
        out List<TenantMembership> memberships,
        out List<TenantAppEntitlement> entitlements,
        GlobalUser owner)
    {
        orgs = [];
        memberships = [];
        entitlements = [];

        var orgList = orgs;
        var membershipList = memberships;
        var entitlementList = entitlements;

        repo.GetUserByIdAsync(owner.Id, Arg.Any<CancellationToken>()).Returns(owner);
        repo.GetUserByIdAsync(Arg.Is<Guid>(id => id != owner.Id), Arg.Any<CancellationToken>())
            .Returns((GlobalUser?)null);

        repo.When(r => r.AddOrganization(Arg.Any<Organization>()))
            .Do(ci => orgList.Add(ci.Arg<Organization>()));
        repo.When(r => r.AddTenantMembership(Arg.Any<TenantMembership>()))
            .Do(ci => membershipList.Add(ci.Arg<TenantMembership>()));
        repo.When(r => r.AddEntitlement(Arg.Any<TenantAppEntitlement>()))
            .Do(ci => entitlementList.Add(ci.Arg<TenantAppEntitlement>()));

        repo.IsSlugUniqueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var slug = ci.ArgAt<string>(0);
                return Task.FromResult(orgList.All(o => o.Slug != slug));
            });

        var eventBus = Substitute.For<IEventBus>();
        return new CreateWorkspaceCommandHandler(repo, eventBus);
    }

    [Test]
    public async Task Authenticated_User_With_Zero_Memberships_Creates_Admin_Workspace()
    {
        var owner = new GlobalUser("solo@example.com", "Solo", "hash");
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var orgs, out var memberships, out var entitlements, owner);

        var id = await handler.Handle(
            new CreateWorkspaceCommand(owner.Id, "Second Shop", "Second-Shop", [.. CoreApps]),
            CancellationToken.None);

        Assert.That(id, Is.EqualTo(orgs[0].Id));
        Assert.That(orgs[0].Slug, Is.EqualTo("second-shop"));
        Assert.That(orgs[0].IsActive, Is.True);
        Assert.That(memberships, Has.Count.EqualTo(1));
        Assert.That(memberships[0].GlobalUserId, Is.EqualTo(owner.Id));
        Assert.That(memberships[0].Role, Is.EqualTo("ADMIN"));
        Assert.That(entitlements.Select(e => e.AppId), Is.EquivalentTo(CoreApps));
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void Duplicate_Slug_Throws_And_Writes_Nothing()
    {
        var owner = new GlobalUser("solo@example.com", "Solo", "hash");
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var orgs, out var memberships, out _, owner);
        orgs.Add(new Organization("Existing", "taken-slug"));

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateWorkspaceCommand(owner.Id, "New", "taken-slug", [.. CoreApps]),
            CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("already taken"));
        Assert.That(memberships, Is.Empty);
        repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [TestCase("admin")]
    [TestCase("login")]
    public void Reserved_Slug_Throws_Business_Rule(string slug)
    {
        var owner = new GlobalUser("solo@example.com", "Solo", "hash");
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out var orgs, out _, out _, owner);

        Assert.ThrowsAsync<BusinessRuleValidationException>(() => handler.Handle(
            new CreateWorkspaceCommand(owner.Id, "New", slug, [.. CoreApps]),
            CancellationToken.None));

        Assert.That(orgs, Is.Empty);
        repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void Missing_User_Throws()
    {
        var owner = new GlobalUser("solo@example.com", "Solo", "hash");
        var repo = Substitute.For<IOneRepository>();
        var handler = CreateHandler(repo, out _, out _, out _, owner);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateWorkspaceCommand(Guid.CreateVersion7(), "New", "new-shop", [.. CoreApps]),
            CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("User not found"));
    }
}
