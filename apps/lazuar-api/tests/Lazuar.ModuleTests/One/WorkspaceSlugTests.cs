using System;
using Microsoft.EntityFrameworkCore;
using Modules.One.Application;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class WorkspaceSlugTests
{
    [Test]
    public void LooksLikeUniqueViolation_MapsPostgresSlugIndex()
    {
        var inner = new InvalidOperationException(
            "23505: duplicate key value violates unique constraint \"IX_Organizations_Slug\"");
        var wrap = new DbUpdateException("could not save", inner);
        Assert.That(WorkspaceSlug.LooksLikeUniqueViolation(wrap), Is.True);
        Assert.That(WorkspaceSlug.LooksLikeUniqueViolation(new InvalidOperationException("other")), Is.False);
    }
}
