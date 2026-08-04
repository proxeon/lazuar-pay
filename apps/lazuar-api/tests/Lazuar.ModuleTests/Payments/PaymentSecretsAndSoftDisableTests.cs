using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Modules.Payments.Application.Ports;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Infrastructure.Queries;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class PaymentSecretsAndSoftDisableTests
{
    private static ISecretVault CreateVault() =>
        new AesSecretVault(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kms:MasterKey"] = "test-master-key-for-unit-tests-32"
            })
            .Build());

    [Test]
    public void TenantPaymentConfiguration_EncryptRoundTrip_ViaVault()
    {
        var vault = CreateVault();
        var plainKey = "sk_live_example_secret_value";
        var plainWh = "whsec_example_signing_secret";

        var encryptedKey = vault.Encrypt(plainKey);
        var encryptedWh = vault.Encrypt(plainWh);

        encryptedKey.Should().NotBe(plainKey);
        encryptedWh.Should().NotBe(plainWh);

        var config = new TenantPaymentConfiguration(
            Guid.CreateVersion7(),
            "STRIPE",
            encryptedKey,
            encryptedWh,
            null,
            isActive: true);

        vault.Decrypt(config.ApiKey!).Should().Be(plainKey);
        vault.Decrypt(config.WebhookSecret!).Should().Be(plainWh);
    }

    [Test]
    public void SoftDisable_SetActive_PreservesCredentials()
    {
        var orgId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(orgId, "BILLPLZ", "enc-api", "enc-wh", "coll_1", isActive: true);

        config.SetActive(false);

        config.IsActive.Should().BeFalse();
        config.ApiKey.Should().Be("enc-api");
        config.WebhookSecret.Should().Be("enc-wh");
        config.MerchantId.Should().Be("coll_1");

        config.SetActive(true);
        config.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task GetPaymentConfig_MasksSecrets_WithHasFlagsAndHints()
    {
        var vault = CreateVault();
        var orgId = Guid.CreateVersion7();
        var plain = "sk_test_ABCDEFGH1234";
        var wh = "whsec_XYZ9876543210";

        var config = new TenantPaymentConfiguration(
            orgId,
            "STRIPE",
            vault.Encrypt(plain),
            vault.Encrypt(wh),
            null,
            isActive: true);

        var repo = Substitute.For<ITenantPaymentConfigRepository>();
        repo.GetAllByTenantIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration> { config });

        var handler = new GetPaymentConfigQueryHandler(repo, vault);
        var result = (await handler.Handle(new Modules.Payments.Contracts.Queries.GetPaymentConfigQuery(orgId), CancellationToken.None)).ToList();

        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Gateway_type.Should().Be("STRIPE");
        dto.Is_active.Should().BeTrue();
        dto.Has_api_key.Should().BeTrue();
        dto.Has_secret_key.Should().BeTrue();
        dto.Has_webhook_secret.Should().BeTrue();
        dto.Api_key.Should().BeNull();
        dto.Secret_key.Should().BeNull();
        dto.Webhook_secret.Should().BeNull();
        dto.Api_key_hint.Should().Be($"…{plain[^4..]}");
        dto.Secret_key_hint.Should().Be($"…{plain[^4..]}");
        dto.Webhook_secret_hint.Should().Be($"…{wh[^4..]}");
    }

    [Test]
    public void DecryptOrPlaintext_AcceptsLegacyPlaintextRows()
    {
        var vault = CreateVault();
        vault.DecryptOrPlaintext("sk_legacy_plain").Should().Be("sk_legacy_plain");
    }

    [Test]
    public void IsKeepExistingSecret_DetectsEmptyAndMask()
    {
        SecretVaultExtensions.IsKeepExistingSecret(null).Should().BeTrue();
        SecretVaultExtensions.IsKeepExistingSecret("").Should().BeTrue();
        SecretVaultExtensions.IsKeepExistingSecret("  ").Should().BeTrue();
        SecretVaultExtensions.IsKeepExistingSecret("••••••••abcd").Should().BeTrue();
        SecretVaultExtensions.IsKeepExistingSecret("sk_new").Should().BeFalse();
    }
}
