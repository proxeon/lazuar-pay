using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class RemoveWorkspaceMemberCommandHandlerTests
{
    [Test]
    public async Task Remove_LastAdmin_Throws()
    {
        var orgId = Guid.CreateVersion7();
        var admin = new GlobalUser("admin@example.com", "Admin", "hash");
        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(admin.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(admin.Id, orgId, "ADMIN"));
        repo.GetUserByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        repo.CountManagingMembersAsync(orgId, Arg.Any<CancellationToken>()).Returns(1);

        var handler = new RemoveWorkspaceMemberCommandHandler(repo);
        var act = () => handler.Handle(
            new RemoveWorkspaceMemberCommand(orgId, admin.Id, admin.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*last admin*");
        repo.DidNotReceive().RemoveTenantMembership(Arg.Any<TenantMembership>());
    }

    [Test]
    public async Task Remove_Member_WhenAnotherAdminExists_Succeeds()
    {
        var orgId = Guid.CreateVersion7();
        var admin = new GlobalUser("admin@example.com", "Admin", "hash");
        var member = new GlobalUser("member@example.com", "Member", "hash");
        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(admin.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(admin.Id, orgId, "ADMIN"));
        repo.GetMembershipAsync(member.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(member.Id, orgId, "MEMBER"));
        repo.GetUserByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        var handler = new RemoveWorkspaceMemberCommandHandler(repo);
        await handler.Handle(
            new RemoveWorkspaceMemberCommand(orgId, admin.Id, member.Id),
            CancellationToken.None);

        repo.Received(1).RemoveTenantMembership(Arg.Any<TenantMembership>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
