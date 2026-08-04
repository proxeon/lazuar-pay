using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class LhdnSecretsVaultTests
{
    private static IConfiguration CreateConfig(string masterKey = "test-master-key-for-unit-tests-32") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kms:MasterKey"] = masterKey
            })
            .Build();

    [Test]
    public void CertificateVault_EncryptsPfxBytesAndPassword_RoundTrips()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=LazuarTest", ecdsa, HashAlgorithmName.SHA256);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var password = "test-pfx-passphrase";
        var pfxBytes = cert.Export(X509ContentType.Pkcs12, password);
        var base64Pfx = Convert.ToBase64String(pfxBytes);

        var vault = new CertificateVaultService(CreateConfig());
        var (encryptedPfx, passwordCipher) = vault.EncryptCertificate(base64Pfx, password);

        encryptedPfx.Should().NotBe(base64Pfx);
        passwordCipher.Should().NotBe(password);

        using var loaded = vault.GetDecryptedCertificate(encryptedPfx, passwordCipher);
        loaded.Subject.Should().Contain("LazuarTest");
        loaded.HasPrivateKey.Should().BeTrue();
    }

    [Test]
    public void CertificateVault_LegacyPlaintextPfx_StillLoads()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=LegacyPfx", ecdsa, HashAlgorithmName.SHA256);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var password = "legacy-pass";
        var pfxBytes = cert.Export(X509ContentType.Pkcs12, password);
        var base64Pfx = Convert.ToBase64String(pfxBytes);

        // Password encrypted the old way; PFX stored as raw base64 (legacy).
        var vault = new CertificateVaultService(CreateConfig());
        var (_, passwordCipher) = vault.EncryptCertificate(base64Pfx, password);

        using var loaded = vault.GetDecryptedCertificate(base64Pfx, passwordCipher);
        loaded.Subject.Should().Contain("LegacyPfx");
    }

    [Test]
    public void AesSecretVault_EncryptsClientSecret()
    {
        var vault = new AesSecretVault(CreateConfig());
        var secret = "myinvois-client-secret-value";
        var cipher = vault.Encrypt(secret);
        cipher.Should().NotBe(secret);
        vault.Decrypt(cipher).Should().Be(secret);
    }
}
