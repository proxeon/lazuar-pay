using FluentAssertions;
using Modules.Messaging.Infrastructure.Email;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Messaging;

[TestFixture]
public class EmailTemplateBuilderTests
{
    [Test]
    public void WrapWithBrandHtml_Empty_ReturnsEmpty()
    {
        EmailTemplateBuilder.WrapWithBrandHtml("").Should().BeEmpty();
        EmailTemplateBuilder.WrapWithBrandHtml("   ").Should().BeEmpty();
    }

    [Test]
    public void WrapWithBrandHtml_IncludesBodyAndBrandFooter()
    {
        var html = EmailTemplateBuilder.WrapWithBrandHtml("Hello\nWorld");

        html.Should().Contain("Hello<br/>World");
        html.Should().Contain("Powered by");
        html.Should().Contain("Lazuar");
        html.Should().NotContain("Unsubscribe");
    }

    [Test]
    public void WrapWithBrandHtml_WithUnsubscribe_AddsFooterLink()
    {
        var url = "https://example.com/unsub";
        var html = EmailTemplateBuilder.WrapWithBrandHtml("<p>Hi</p>", url);

        html.Should().Contain(url);
        html.Should().Contain("Unsubscribe");
        html.Should().Contain("Powered by");
    }
}
