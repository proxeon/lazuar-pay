using System;
using FluentAssertions;
using Modules.CRM.Contracts;
using Modules.CRM.Domain;
using NUnit.Framework;

namespace Lazuar.ModuleTests.CRM;

[TestFixture]
public class ClientProfileAnonymizedEventTests
{
    [Test]
    public void Event_CarriesPreWipeContactDetails()
    {
        var orgId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var @event = new ClientProfileAnonymizedIntegrationEvent(
            orgId, profileId, "user@example.com", "60123456789");

        @event.OrganizationId.Should().Be(orgId);
        @event.ClientProfileId.Should().Be(profileId);
        @event.Email.Should().Be("user@example.com");
        @event.Phone.Should().Be("60123456789");
        @event.Id.Should().NotBe(Guid.Empty);
    }

    [Test]
    public void Anonymize_WipesPiiAndConsent()
    {
        var profile = new ClientProfileEntity
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            FullName = "Ahmad",
            Email = "ahmad@example.com",
            Phone = "60111111111",
            Tin = "IG123",
            ConsentedToMarketing = true
        };

        profile.Anonymize();

        profile.FullName.Should().Be("Anonymized User");
        profile.Email.Should().StartWith("deleted_");
        profile.Phone.Should().BeEmpty();
        profile.Tin.Should().BeNull();
        profile.ConsentedToMarketing.Should().BeFalse();
    }

    [Test]
    public void CreateAndResolveCommands_DefaultConsentFalse()
    {
        var create = new CreateClientProfileCommand(
            Guid.CreateVersion7(), "Name", "a@b.com", "601");
        create.ConsentedToMarketing.Should().BeFalse();

        var resolve = new ResolveClientProfileCommand(
            Guid.CreateVersion7(), "Name", "a@b.com", "601");
        resolve.ConsentedToMarketing.Should().BeFalse();
    }
}
