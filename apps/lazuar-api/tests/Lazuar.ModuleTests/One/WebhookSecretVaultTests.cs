using System.Collections.Generic;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class WebhookSecretVaultTests
{
    private static ISecretVault CreateVault() =>
        new AesSecretVault(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kms:MasterKey"] = "test-master-key-for-unit-tests-32"
            })
            .Build());

    [Test]
    public void DecryptOrPlaintext_Of_Encrypted_Whsec_Equals_Plain()
    {
        var vault = CreateVault();
        const string plain = "whsec_roundtrip_secret";
        var cipher = vault.Encrypt(plain);

        cipher.Should().NotStartWith("whsec_");
        vault.DecryptOrPlaintext(cipher).Should().Be(plain);
    }

    [Test]
    public void DecryptOrPlaintext_Accepts_Legacy_Plaintext_Whsec()
    {
        var vault = CreateVault();
        vault.DecryptOrPlaintext("whsec_legacy_row").Should().Be("whsec_legacy_row");
    }
}
