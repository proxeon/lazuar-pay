using System;
using BuildingBlocks.Application;
using Modules.Communications.Infrastructure.Services;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class TenantEmailKeyTests
{
    [Test]
    public void Decryptable_Key_Is_Usable()
    {
        var vault = Substitute.For<ISecretVault>();
        vault.Decrypt("cipher").Returns("re_live_abc");

        Assert.That(TenantEmailKey.TryResolve(vault, "cipher", out var plain), Is.True);
        Assert.That(plain, Is.EqualTo("re_live_abc"));
    }

    [Test]
    public void Legacy_Plaintext_Resend_Key_Is_Usable_When_Decrypt_Fails()
    {
        var vault = Substitute.For<ISecretVault>();
        vault.Decrypt("re_legacy_plain").Returns(_ => throw new InvalidOperationException("bad blob"));

        Assert.That(TenantEmailKey.TryResolve(vault, "re_legacy_plain", out var plain), Is.True);
        Assert.That(plain, Is.EqualTo("re_legacy_plain"));
    }

    [Test]
    public void Undecryptable_Garbage_Is_Not_A_Live_Key()
    {
        var vault = Substitute.For<ISecretVault>();
        vault.Decrypt("kms-revoked-blob").Returns(_ => throw new InvalidOperationException("cannot decrypt"));

        Assert.That(TenantEmailKey.TryResolve(vault, "kms-revoked-blob", out var plain), Is.False);
        Assert.That(plain, Is.EqualTo(""));
    }

    [Test]
    public void Empty_Stored_Key_Is_Not_Usable()
    {
        var vault = Substitute.For<ISecretVault>();
        Assert.That(TenantEmailKey.TryResolve(vault, "", out _), Is.False);
        Assert.That(TenantEmailKey.TryResolve(vault, "   ", out _), Is.False);
    }
}
