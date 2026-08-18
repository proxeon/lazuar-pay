using System.Linq;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Lhdn.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class TaxDocumentInternalReferenceIndexTests
{
    [Test]
    public void TaxDocument_InternalReferenceId_IsUniquePerOrganization()
    {
        using var db = new LhdnDbContext(
            InMemoryDb.CreateOptions<LhdnDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var entity = db.Model.FindEntityType(typeof(TaxDocument));
        Assert.That(entity, Is.Not.Null);
        var index = entity!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "OrganizationId", "InternalReferenceId" }));
        Assert.That(index.IsUnique, Is.True);
    }
}
