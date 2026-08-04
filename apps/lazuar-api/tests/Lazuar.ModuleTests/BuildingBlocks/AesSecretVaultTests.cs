using System.Collections.Generic;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class AesSecretVaultTests
{
    private static AesSecretVault CreateVault(string masterKey = "test-master-key-for-unit-tests-32")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kms:MasterKey"] = masterKey
            })
            .Build();
        return new AesSecretVault(config);
    }

    [Test]
    public void EncryptDecrypt_RoundTripsPlaintext()
    {
        var vault = CreateVault();
        var plain = "re_test_api_key_secret_value";

        var cipher = vault.Encrypt(plain);
        cipher.Should().NotBe(plain);
        cipher.Should().NotBeNullOrWhiteSpace();

        vault.Decrypt(cipher).Should().Be(plain);
    }

    [Test]
    public void Encrypt_ProducesDifferentCiphertextsForSamePlaintext()
    {
        var vault = CreateVault();
        var plain = "re_same_key";

        var a = vault.Encrypt(plain);
        var b = vault.Encrypt(plain);

        a.Should().NotBe(b);
        vault.Decrypt(a).Should().Be(plain);
        vault.Decrypt(b).Should().Be(plain);
    }

    [Test]
    public void Decrypt_WithWrongKey_Throws()
    {
        var vault1 = CreateVault("key-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var vault2 = CreateVault("key-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var cipher = vault1.Encrypt("secret");

        var act = () => vault2.Decrypt(cipher);
        act.Should().Throw<Exception>();
    }
}
