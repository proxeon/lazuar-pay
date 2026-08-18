using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.One.Application;
using Modules.One.Application.Commands;
using Modules.One.Contracts;
using Modules.One.Contracts.Events;
using Modules.One.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class ArchiveWorkspaceCommandHandlerTests
{
    [Test]
    public async Task Archive_RevokesKeys_DropsMemberships_AndPublishesTenantInactive()
    {
        var org = new Organization("Studio", "studio");
        var admin = new GlobalUser("admin@example.com", "Admin", "hash");
        var member = new TenantMembership(admin.Id, org.Id, "ADMIN");
        var key = new ApiCredential(org.Id, "live", "sk_live_", "hash", "abcd", "lhdn.documents:write");
        var invite = new WorkspaceInvitation(org.Id, "new@example.com", "MEMBER", "h", "p", DateTime.UtcNow.AddDays(1));

        var repo = Substitute.For<IOneRepository>();
        repo.GetMembershipAsync(admin.Id, org.Id, Arg.Any<CancellationToken>()).Returns(member);
        repo.GetOrganizationByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        repo.ListApiCredentialsAsync(org.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ApiCredential> { key });
        repo.ListPendingInvitationsAsync(org.Id, Arg.Any<CancellationToken>())
            .Returns(new List<WorkspaceInvitation> { invite });
        repo.ListMembershipsAsync(org.Id, Arg.Any<CancellationToken>())
            .Returns(new List<TenantMembership> { member });

        var bus = Substitute.For<IEventBus>();
        var handler = new ArchiveWorkspaceCommandHandler(repo, bus);

        await handler.Handle(new ArchiveWorkspaceCommand(org.Id, admin.Id), CancellationToken.None);

        org.IsActive.Should().BeFalse();
        key.IsActive.Should().BeFalse();
        invite.Status.Should().Be("REVOKED");
        repo.Received(1).RemoveTenantMembership(member);
        await bus.Received(1).PublishAsync(Arg.Is<ApiKeyRevokedIntegrationEvent>(e =>
            e.OrganizationId == org.Id && e.KeyHash == "hash"));
        await bus.Received(1).PublishAsync(Arg.Is<TenantUpdatedIntegrationEvent>(e =>
            e.TenantId == org.Id && e.IsActive == false));
    }
}
