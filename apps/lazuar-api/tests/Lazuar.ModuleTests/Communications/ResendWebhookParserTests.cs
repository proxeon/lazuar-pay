using System;
using FluentAssertions;
using Modules.Communications.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class ResendWebhookParserTests
{
    [Test]
    public void Parses_Data_To_And_Object_Tags()
    {
        var org = Guid.CreateVersion7();
        var ok = ResendWebhookParser.TryParseSuppression(
            "{\"type\":\"email.bounced\",\"data\":{\"to\":[\"user@example.com\"],\"tags\":{\"org\":\"" + org + "\"}}}",
            out var type,
            out var recipient,
            out var parsedOrg);

        ok.Should().BeTrue();
        type.Should().Be("email.bounced");
        recipient.Should().Be("user@example.com");
        parsedOrg.Should().Be(org);
        ResendWebhookParser.MapReason(type).Should().Be("BOUNCE");
    }

    [Test]
    public void Parses_Array_Tags()
    {
        var org = Guid.CreateVersion7();
        ResendWebhookParser.TryParseSuppression(
            "{\"type\":\"email.complained\",\"data\":{\"email\":{\"to\":[\"a@b.com\"]},\"tags\":[{\"name\":\"org\",\"value\":\"" + org + "\"}]}}",
            out _,
            out var recipient,
            out var parsedOrg);

        recipient.Should().Be("a@b.com");
        parsedOrg.Should().Be(org);
        ResendWebhookParser.MapReason("email.complained").Should().Be("COMPLAINT");
    }

    [Test]
    public void Missing_Org_Tag_Leaves_Org_Null()
    {
        ResendWebhookParser.TryParseSuppression(
            """{"type":"email.bounced","data":{"to":["user@example.com"]}}""",
            out _,
            out var recipient,
            out var org);

        recipient.Should().Be("user@example.com");
        org.Should().BeNull();
    }
}
