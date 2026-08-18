using BuildingBlocks.Application;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class MarkdownParserTests
{
    [Test]
    public void ToHtml_Does_Not_Emit_Raw_Html_Tags()
    {
        var html = MarkdownParser.ToHtml("Hello <img src=x onerror=alert(1)> world");
        Assert.That(html, Does.Not.Contain("<img"));
        Assert.That(html, Does.Contain("&lt;img"));
    }
}
