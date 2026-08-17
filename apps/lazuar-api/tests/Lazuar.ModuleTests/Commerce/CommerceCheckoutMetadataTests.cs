using System;
using System.Collections.Generic;
using FluentAssertions;
using Modules.Commerce.Application;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceCheckoutMetadataTests
{
    [Test]
    public void MergeClientIntoGateway_PreservesAuraOrgId_AndOverwritesTypeWhenSaas()
    {
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var auraOrgId = Guid.CreateVersion7();

        var merged = CommerceCheckoutMetadata.MergeClientIntoGateway(
            new Dictionary<string, string>
            {
                ["aura_org_id"] = auraOrgId.ToString(),
                ["type"] = "saas_subscription",
                ["billing_interval"] = "monthly"
            },
            tenantId,
            sessionId);

        merged["type"].Should().Be(CommerceCheckoutMetadata.TypeSaas);
        merged["aura_org_id"].Should().Be(auraOrgId.ToString());
        merged["tenant_id"].Should().Be(tenantId.ToString());
        merged["subscription_id"].Should().Be(sessionId.ToString());
        merged["billing_interval"].Should().Be("monthly");
    }

    [Test]
    public void MergeClientIntoGateway_DefaultsTypeToCommerce()
    {
        var merged = CommerceCheckoutMetadata.MergeClientIntoGateway(
            null, Guid.CreateVersion7(), Guid.CreateVersion7());
        merged["type"].Should().Be(CommerceCheckoutMetadata.TypeCommerce);
    }

    [Test]
    public void ForPersistence_DropsCorrelationStamps_AndDerivesInterval()
    {
        var auraOrgId = Guid.CreateVersion7();
        var persist = CommerceCheckoutMetadata.ForPersistence(
            new Dictionary<string, string>
            {
                ["aura_org_id"] = auraOrgId.ToString(),
                ["type"] = "saas_subscription",
                ["subscription_id"] = Guid.CreateVersion7().ToString(),
                ["tenant_id"] = Guid.CreateVersion7().ToString()
            },
            "yr");

        persist.Should().ContainKey("aura_org_id");
        persist.Should().NotContainKey("subscription_id");
        persist.Should().NotContainKey("tenant_id");
        persist["billing_interval"].Should().Be("yearly");
        persist["type"].Should().Be(CommerceCheckoutMetadata.TypeSaas);
    }

    [Test]
    public void IsCommerceSubscriptionType_AcceptsSaasAlias()
    {
        CommerceCheckoutMetadata.IsCommerceSubscriptionType("saas_subscription").Should().BeTrue();
        CommerceCheckoutMetadata.IsCommerceSubscriptionType("commerce_subscription").Should().BeTrue();
        CommerceCheckoutMetadata.IsCommerceSubscriptionType("trial").Should().BeTrue();
        CommerceCheckoutMetadata.IsCommerceSubscriptionType("custom_payment_link").Should().BeFalse();
    }
}
