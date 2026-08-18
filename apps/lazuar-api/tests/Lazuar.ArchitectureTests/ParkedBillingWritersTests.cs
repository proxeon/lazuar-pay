using System.IO;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class ParkedBillingWritersTests
{
    [Test]
    public void RevenueRecognitionJob_IsNotHosted()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Billing", "Infrastructure", "DependencyInjection.cs"));
        Assert.That(File.Exists(path), Is.True, path);
        var text = File.ReadAllText(path);
        Assert.That(text, Does.Contain("// services.AddHostedService<RevenueRecognitionJob>()"));
        Assert.That(
            System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"(?m)^\s*services\.AddHostedService<RevenueRecognitionJob>\(\);"),
            Is.False,
            "RevenueRecognitionJob must stay commented out of DI.");
    }
}
