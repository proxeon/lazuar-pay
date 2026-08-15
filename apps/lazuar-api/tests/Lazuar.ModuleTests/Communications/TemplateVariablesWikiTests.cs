using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.Communications.Infrastructure.Services;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class TemplateVariablesWikiTests
{
    [Test]
    public async Task GetTemplateVariables_ListsDunningTags_AndOmitsCommunityLeftovers()
    {
        var svc = new CommunicationsQueryService(
            Substitute.For<ISqlConnectionFactory>(),
            Substitute.For<ISecretVault>());

        var tags = (await svc.GetTemplateVariablesAsync())
            .SelectMany(c => c.Items)
            .Select(i => i.Tag)
            .ToList();

        tags.Should().Contain("{{business_name}}");
        tags.Should().Contain("{{amount}}");
        tags.Should().Contain("{{currency}}");
        tags.Should().Contain("{{days_overdue}}");
        tags.Should().Contain("{{update_payment_link}}");
        tags.Should().Contain("{{renewal_link}}");
        tags.Should().Contain("{{current_period_end}}");
        tags.Should().NotContain("{{meeting_link}}");
        tags.Should().NotContain("{{group_link}}");
    }
}
