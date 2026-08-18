using System.IO;
using System.Linq;
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

    [Test]
    public void ManualPaymentRecorded_IsNotPublishedInProduction()
    {
        var root = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", ".."));
        var hits = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(p => File.ReadAllLines(p).Select((line, i) => (p, i, line)))
            .Where(x => x.line.Contains("new ManualPaymentRecordedIntegrationEvent", StringComparison.Ordinal))
            .ToList();

        Assert.That(hits, Is.Empty, "Do not publish ManualPaymentRecorded; enrollment journals cash.");
    }
}
