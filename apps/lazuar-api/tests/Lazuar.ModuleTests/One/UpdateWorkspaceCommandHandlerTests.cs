using System;
using System.Threading;
using System.Threading.Tasks;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class UpdateWorkspaceCommandHandlerTests
{
    [Test]
    public async Task SuperAdmin_Membership_Can_Update()
    {
        var orgId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var org = new Organization("Acme", $"acme-{Guid.CreateVersion7():N}"[..20]);
        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(userId, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(userId, orgId, "SUPER_ADMIN"));
        repo.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>()).Returns(org);

        var handler = new UpdateWorkspaceCommandHandler(repo);
        await handler.Handle(new UpdateWorkspaceCommand(orgId, userId, "Acme 2", org.Slug), CancellationToken.None);

        Assert.That(org.Name, Is.EqualTo("Acme 2"));
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void Member_Cannot_Update()
    {
        var orgId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(userId, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(userId, orgId, "MEMBER"));

        var handler = new UpdateWorkspaceCommandHandler(repo);
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdateWorkspaceCommand(orgId, userId, "Nope", "nope"), CancellationToken.None));
    }
}
