using System;
using FluentAssertions;
using Modules.Communications.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class TenantEmailConfigurationTests
{
    [Test]
    public void UpdateWithoutKey_PreservesEncryptedApiKey()
    {
        var orgId = Guid.CreateVersion7();
        var config = new TenantEmailConfiguration(orgId, "encrypted-blob", "from@example.com", true);

        config.UpdateWithoutKey("new@example.com", false);

        config.ApiKey.Should().Be("encrypted-blob");
        config.SenderEmail.Should().Be("new@example.com");
        config.IsActive.Should().BeFalse();
    }

    [Test]
    public void UpdateConfiguration_ReplacesKeyAndSender()
    {
        var config = new TenantEmailConfiguration(Guid.CreateVersion7(), "old", "a@b.com", true);
        config.UpdateConfiguration("new-encrypted", "c@d.com", true);

        config.ApiKey.Should().Be("new-encrypted");
        config.SenderEmail.Should().Be("c@d.com");
    }
}
