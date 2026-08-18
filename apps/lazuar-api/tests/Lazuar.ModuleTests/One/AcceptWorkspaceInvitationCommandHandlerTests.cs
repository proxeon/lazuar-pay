using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class AcceptWorkspaceInvitationCommandHandlerTests
{
    [Test]
    public async Task Accept_PendingMatchingEmail_CreatesMembership()
    {
        var orgId = Guid.CreateVersion7();
        var user = new GlobalUser("staff@example.com", "Staff", "hash");
        var invitation = new WorkspaceInvitation(
            orgId, "staff@example.com", "MEMBER", "token-hash", "plain", DateTime.UtcNow.AddDays(7));
        TenantMembership? membership = null;

        var repo = Substitute.For<IOneRepository>();
        repo.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        repo.GetInvitationByHashAsync("token-hash", Arg.Any<CancellationToken>()).Returns(invitation);
        repo.When(r => r.AddTenantMembership(Arg.Any<TenantMembership>()))
            .Do(ci => membership = ci.Arg<TenantMembership>());

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.HashToken("plain").Returns("token-hash");

        var handler = new AcceptWorkspaceInvitationCommandHandler(repo, tokens);
        await handler.Handle(new AcceptWorkspaceInvitationCommand(user.Id, "plain"), CancellationToken.None);

        invitation.Status.Should().Be("ACCEPTED");
        membership.Should().NotBeNull();
        membership!.GlobalUserId.Should().Be(user.Id);
        membership.OrganizationId.Should().Be(orgId);
        membership.Role.Should().Be("MEMBER");
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Accept_ExpiredInvite_Throws()
    {
        var user = new GlobalUser("staff@example.com", "Staff", "hash");
        var invitation = new WorkspaceInvitation(
            Guid.CreateVersion7(), "staff@example.com", "MEMBER", "token-hash", "plain", DateTime.UtcNow.AddHours(-1));

        var repo = Substitute.For<IOneRepository>();
        repo.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        repo.GetInvitationByHashAsync("token-hash", Arg.Any<CancellationToken>()).Returns(invitation);

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.HashToken("plain").Returns("token-hash");

        var handler = new AcceptWorkspaceInvitationCommandHandler(repo, tokens);
        var act = () => handler.Handle(new AcceptWorkspaceInvitationCommand(user.Id, "plain"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*invalid or expired*");
        await repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Accept_WrongEmail_Throws()
    {
        var user = new GlobalUser("other@example.com", "Other", "hash");
        var invitation = new WorkspaceInvitation(
            Guid.CreateVersion7(), "invited@example.com", "MEMBER", "token-hash", "plain", DateTime.UtcNow.AddDays(7));

        var repo = Substitute.For<IOneRepository>();
        repo.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        repo.GetInvitationByHashAsync("token-hash", Arg.Any<CancellationToken>()).Returns(invitation);

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.HashToken("plain").Returns("token-hash");

        var handler = new AcceptWorkspaceInvitationCommandHandler(repo, tokens);
        var act = () => handler.Handle(new AcceptWorkspaceInvitationCommand(user.Id, "plain"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different email*");
        invitation.Status.Should().Be("PENDING");
        await repo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Accept_AlreadyMember_Is400AndDoesNotInsert()
    {
        var orgId = Guid.CreateVersion7();
        var user = new GlobalUser("staff@example.com", "Staff", "hash");
        var invitation = new WorkspaceInvitation(
            orgId, "staff@example.com", "MEMBER", "token-hash", "plain", DateTime.UtcNow.AddDays(7));

        var repo = Substitute.For<IOneRepository>();
        repo.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        repo.GetInvitationByHashAsync("token-hash", Arg.Any<CancellationToken>()).Returns(invitation);
        repo.HasMembershipAsync(user.Id, orgId, Arg.Any<CancellationToken>()).Returns(true);

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.HashToken("plain").Returns("token-hash");

        var handler = new AcceptWorkspaceInvitationCommandHandler(repo, tokens);
        var act = () => handler.Handle(new AcceptWorkspaceInvitationCommand(user.Id, "plain"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already a member*");
        invitation.Status.Should().Be("ACCEPTED");
        repo.DidNotReceive().AddTenantMembership(Arg.Any<TenantMembership>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
