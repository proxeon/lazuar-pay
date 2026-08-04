using System;
using FluentAssertions;
using Modules.Messaging.Domain;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Messaging;

[TestFixture]
public class MessageDeliveryLogTests
{
    [Test]
    public void Constructor_SetsSentFields()
    {
        var orgId = Guid.CreateVersion7();
        var corr = Guid.CreateVersion7();
        var log = new MessageDeliveryLog(
            orgId,
            "EMAIL",
            "user@example.com",
            "SENT",
            "re_abc123",
            null,
            corr);

        log.OrganizationId.Should().Be(orgId);
        log.Channel.Should().Be("EMAIL");
        log.Recipient.Should().Be("user@example.com");
        log.Status.Should().Be("SENT");
        log.ProviderMessageId.Should().Be("re_abc123");
        log.Error.Should().BeNull();
        log.CorrelationEventId.Should().Be(corr);
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Constructor_SetsFailedAndSkipped()
    {
        var failed = new MessageDeliveryLog(Guid.CreateVersion7(), "EMAIL", "a@b.com", "FAILED", null, "timeout");
        failed.Status.Should().Be("FAILED");
        failed.Error.Should().Be("timeout");

        var skipped = new MessageDeliveryLog(Guid.CreateVersion7(), "WHATSAPP", "+6012", "SKIPPED", null, "WhatsApp channel disabled");
        skipped.Status.Should().Be("SKIPPED");
    }
}
