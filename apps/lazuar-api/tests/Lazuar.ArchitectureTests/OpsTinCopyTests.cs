using System.IO;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class OpsTinCopyTests
{
    [Test]
    public void ProductForms_SayCheckoutValidatesTin_NotThatTheySkipIt()
    {
        var opsSrc = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "..",
            "lazuar-ops", "src"));
        Assert.That(Directory.Exists(opsSrc), Is.True, opsSrc);

        var files = new[]
        {
            Path.Combine(opsSrc, "modules", "commerce", "components", "ProductForm.tsx"),
            Path.Combine(opsSrc, "modules", "commerce", "components", "CreateProductForm.tsx"),
            Path.Combine(opsSrc, "components", "forms", "CreateProductForm.tsx"),
        };

        foreach (var path in files)
        {
            Assert.That(File.Exists(path), Is.True, path);
            var text = File.ReadAllText(path);
            Assert.That(text, Does.Contain("Checkout validates the TIN"), path);
            Assert.That(text, Does.Not.Contain("We do not validate the TIN at checkout"), path);
        }
    }
}
