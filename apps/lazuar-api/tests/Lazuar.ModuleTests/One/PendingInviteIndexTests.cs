using System.Linq;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class PendingInviteIndexTests
{
    [Test]
    public void PendingInvite_OrganizationEmail_IsUnique()
    {
        using var db = new OneDbContext(
            InMemoryDb.CreateOptions<OneDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var entity = db.Model.FindEntityType(typeof(WorkspaceInvitation));
        Assert.That(entity, Is.Not.Null);
        var index = entity!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "OrganizationId", "Email" }));
        Assert.That(index.IsUnique, Is.True);
    }
}
