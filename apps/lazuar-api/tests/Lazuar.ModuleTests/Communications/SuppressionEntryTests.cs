using System;
using FluentAssertions;
using Modules.Communications.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class SuppressionEntryTests
{
    [Test]
    public void Constructor_NormalizesEmailToLowercase()
    {
        var entry = new SuppressionEntry(Guid.NewGuid(), "User@Example.COM", "UNSUBSCRIBE");
        entry.Email.Should().Be("user@example.com");
    }

    [Test]
    public void Constructor_SetsReasonAndSource()
    {
        var entry = new SuppressionEntry(Guid.NewGuid(), "a@b.com", "BOUNCE", "resend_webhook");
        entry.Reason.Should().Be("BOUNCE");
        entry.Source.Should().Be("resend_webhook");
    }

    [Test]
    public void Constructor_SourceDefaultsToNull()
    {
        var entry = new SuppressionEntry(Guid.NewGuid(), "a@b.com", "COMPLAINT");
        entry.Source.Should().BeNull();
    }

    [Test]
    public void Constructor_ThrowsOnEmptyEmail()
    {
        var act = () => new SuppressionEntry(Guid.NewGuid(), "", "UNSUBSCRIBE");
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Constructor_ThrowsOnEmptyReason()
    {
        var act = () => new SuppressionEntry(Guid.NewGuid(), "a@b.com", "");
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Constructor_TrimsWhitespaceInEmail()
    {
        var entry = new SuppressionEntry(Guid.NewGuid(), "  a@b.com  ", "UNSUBSCRIBE");
        entry.Email.Should().Be("a@b.com");
    }
}
