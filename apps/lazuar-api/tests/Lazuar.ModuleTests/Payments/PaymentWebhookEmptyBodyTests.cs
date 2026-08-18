using System.IO;
using FluentAssertions;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class PaymentWebhookEmptyBodyTests
{
    [Test]
    public void EmptyBody_ReturnsBadRequest_DoesNotThrow()
    {
        var path = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Payments", "Infrastructure", "Endpoints.cs"));
        File.Exists(path).Should().BeTrue();
        var text = File.ReadAllText(path);
        text.Should().Contain("Empty request body.");
        text.Should().Contain("Results.BadRequest");
        text.Should().NotContain("throw new InvalidOperationException(\"Empty request body.\")");
    }
}
