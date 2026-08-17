using System;
using FluentAssertions;
using Modules.Lhdn.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class TaxDocumentClaimLeaseTests
{
    [Test]
    public void ClaimProcessingLease_SetsNextPollAt()
    {
        var doc = new TaxDocument(
            Guid.CreateVersion7(),
            "INV-1",
            "hash",
            "<Invoice/>",
            isTestMode: true);

        doc.NextPollAt.Should().BeNull();
        doc.ValidationStatus.Should().Be("PENDING");

        var leaseUntil = DateTime.UtcNow.AddMinutes(2);
        doc.ClaimProcessingLease(leaseUntil);

        doc.NextPollAt.Should().BeCloseTo(leaseUntil, TimeSpan.FromSeconds(1));
        doc.ValidationStatus.Should().Be("PENDING");
    }

    [Test]
    public void ClaimProcessingLease_AfterSubmit_StillMovesNextPollAt()
    {
        var doc = new TaxDocument(Guid.CreateVersion7(), "INV-2", "hash", "<Invoice/>");
        doc.MarkAsSubmitted("sub-uid", "lhdn-uuid");
        var afterSubmit = doc.NextPollAt;

        var leaseUntil = DateTime.UtcNow.AddMinutes(5);
        doc.ClaimProcessingLease(leaseUntil);

        doc.NextPollAt.Should().BeCloseTo(leaseUntil, TimeSpan.FromSeconds(1));
        doc.NextPollAt.Should().NotBe(afterSubmit);
    }

    [Test]
    public void MarkAsValid_WritesPollUuidWhenSubmitMissedIt()
    {
        var doc = new TaxDocument(Guid.CreateVersion7(), "INV-3", "hash", "<Invoice/>");
        doc.MarkAsSubmitted("sub-uid", lhdnUuid: null);
        doc.LhdnUuid.Should().BeNull();

        doc.MarkAsValid("long-id", "poll-uuid");

        doc.LhdnUuid.Should().Be("poll-uuid");
        doc.LongId.Should().Be("long-id");
        doc.ValidationStatus.Should().Be("VALID");
    }
}
