using FluentAssertions;
using Modules.Lhdn.Infrastructure.Gateways;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class LhdnTinValidationBodyTests
{
    [Test]
    public void EmptyBody_IsNotValid()
    {
        var result = LhdnGatewayAdapter.InterpretSuccessTinBody("");
        result.Success.Should().BeTrue();
        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void HtmlBody_IsNotValid()
    {
        var result = LhdnGatewayAdapter.InterpretSuccessTinBody("<html>error</html>");
        result.Success.Should().BeTrue();
        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void JsonObject_IsValid()
    {
        var result = LhdnGatewayAdapter.InterpretSuccessTinBody("""{"name":"Buyer Co"}""");
        result.Success.Should().BeTrue();
        result.IsValid.Should().BeTrue();
        result.TaxpayerName.Should().Be("Buyer Co");
    }
}
