using System;
using FluentAssertions;
using Modules.Commerce.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceHonestyDtoTests
{
    [Test]
    public void ProductMap_SetsSupportsOffSessionFromGateway()
    {
        var stripe = CommerceQueryService.MapToDto(new CommerceQueryService.RawProductDto(
            Guid.CreateVersion7(), "s", "Stripe Plan", 10m, "FIXED", 0m, "MYR", "mo",
            false, false, false, null, true, "STRIPE"));
        stripe.Supports_off_session.Should().BeTrue();

        var billplz = CommerceQueryService.MapToDto(new CommerceQueryService.RawProductDto(
            Guid.CreateVersion7(), "b", "Billplz Plan", 10m, "FIXED", 0m, "MYR", "mo",
            false, false, false, null, true, "BILLPLZ"));
        billplz.Supports_off_session.Should().BeFalse();
    }

    [Test]
    public void SubscriberMap_IncludesIsReminderOnly()
    {
        var raw = new CommerceQueryService.RawSubDto(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Plan",
            10m,
            "ACTIVE",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            DateTime.UtcNow,
            null,
            null,
            true,
            null,
            0,
            null,
            null);

        var dto = CommerceQueryService.MapSubscriberDto(raw, profile: null, DateTime.UtcNow);
        dto.Is_reminder_only.Should().BeTrue();
    }
}
