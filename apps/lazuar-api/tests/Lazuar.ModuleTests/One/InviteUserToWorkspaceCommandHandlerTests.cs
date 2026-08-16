using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Contracts;
using Modules.One.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class InviteUserToWorkspaceCommandHandlerTests
{
    [Test]
    public async Task Invite_Member_StoresUppercaseMember()
    {
        var orgId = Guid.CreateVersion7();
        var admin = new GlobalUser("admin@example.com", "Admin", "hash");
        var adminMembership = new TenantMembership(admin.Id, orgId, "ADMIN");
        WorkspaceInvitation? saved = null;

        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(admin.Id, orgId, Arg.Any<CancellationToken>()).Returns(adminMembership);
        repo.GetUserByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        repo.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((GlobalUser?)null);
        repo.When(r => r.AddWorkspaceInvitation(Arg.Any<WorkspaceInvitation>()))
            .Do(ci => saved = ci.Arg<WorkspaceInvitation>());

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(Arg.Any<int>()).Returns(new GeneratedToken("plain", "hash"));

        var handler = new InviteUserToWorkspaceCommandHandler(repo, tokens);
        var id = await handler.Handle(
            new InviteUserToWorkspaceCommand(orgId, admin.Id, "book@example.com", "member"),
            CancellationToken.None);

        id.Should().Be(saved!.Id);
        saved.Role.Should().Be("MEMBER");
        saved.Email.Should().Be("book@example.com");
    }

    [TestCase("HACKER")]
    [TestCase("banana")]
    [TestCase("CLIENT")]
    public async Task Invite_DisallowedRole_Throws(string role)
    {
        var orgId = Guid.CreateVersion7();
        var admin = new GlobalUser("admin@example.com", "Admin", "hash");
        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(admin.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(admin.Id, orgId, "ADMIN"));
        repo.GetUserByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        var handler = new InviteUserToWorkspaceCommandHandler(repo, Substitute.For<ITokenGeneratorService>());
        var act = () => handler.Handle(
            new InviteUserToWorkspaceCommand(orgId, admin.Id, "x@example.com", role),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ADMIN, MEMBER, or VIEWER*");
    }

    [Test]
    public async Task Member_CannotInvite()
    {
        var orgId = Guid.CreateVersion7();
        var member = new GlobalUser("member@example.com", "Member", "hash");
        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(member.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(member.Id, orgId, "MEMBER"));
        repo.GetUserByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);

        var handler = new InviteUserToWorkspaceCommandHandler(repo, Substitute.For<ITokenGeneratorService>());
        var act = () => handler.Handle(
            new InviteUserToWorkspaceCommand(orgId, member.Id, "x@example.com", "VIEWER"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unauthorized*");
    }

    [Test]
    public async Task SuperAdminMembership_CanInvite()
    {
        var orgId = Guid.CreateVersion7();
        var user = new GlobalUser("owner@example.com", "Owner", "hash");
        WorkspaceInvitation? saved = null;
        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(user.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(user.Id, orgId, "SUPER_ADMIN"));
        repo.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        repo.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((GlobalUser?)null);
        repo.When(r => r.AddWorkspaceInvitation(Arg.Any<WorkspaceInvitation>()))
            .Do(ci => saved = ci.Arg<WorkspaceInvitation>());

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(Arg.Any<int>()).Returns(new GeneratedToken("plain", "hash"));

        var handler = new InviteUserToWorkspaceCommandHandler(repo, tokens);
        await handler.Handle(
            new InviteUserToWorkspaceCommand(orgId, user.Id, "v@example.com", "VIEWER"),
            CancellationToken.None);

        saved!.Role.Should().Be("VIEWER");
    }

    [Test]
    public async Task Invite_RecordsAuditWithoutSecrets()
    {
        var orgId = Guid.CreateVersion7();
        var admin = new GlobalUser("admin@example.com", "Admin", "hash");
        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(admin.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(new TenantMembership(admin.Id, orgId, "ADMIN"));
        repo.GetUserByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        repo.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((GlobalUser?)null);

        var tokens = Substitute.For<ITokenGeneratorService>();
        tokens.GenerateSecureToken(Arg.Any<int>()).Returns(new GeneratedToken("secret-token", "hash"));
        var audit = Substitute.For<IAuditRecorder>();

        var handler = new InviteUserToWorkspaceCommandHandler(repo, tokens, audit);
        await handler.Handle(
            new InviteUserToWorkspaceCommand(orgId, admin.Id, "new@example.com", "MEMBER"),
            CancellationToken.None);

        await audit.Received(1).RecordAsync(
            orgId,
            "member.invited",
            "invitation",
            Arg.Any<string>(),
            Arg.Is<object>(m => m.ToString()!.Contains("new@example.com") && !m.ToString()!.Contains("secret-token")),
            admin.Id,
            admin.Email,
            Arg.Any<CancellationToken>());
    }
}
