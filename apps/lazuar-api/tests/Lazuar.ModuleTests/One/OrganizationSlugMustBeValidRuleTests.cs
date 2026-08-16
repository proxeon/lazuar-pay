using BuildingBlocks.Domain;
using Modules.One.Domain;
using Modules.One.Domain.Rules;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class OrganizationSlugMustBeValidRuleTests
{
    [TestCase("acme")]
    [TestCase("acme-corp")]
    public void Valid_Common_Slugs_Are_Accepted(string slug)
    {
        Assert.That(new OrganizationSlugMustBeValidRule(slug).IsBroken(), Is.False);
    }

    [Test]
    public void Valid_Min_And_Max_Length_Are_Accepted()
    {
        Assert.That(new OrganizationSlugMustBeValidRule(new string('a', 3)).IsBroken(), Is.False);
        Assert.That(new OrganizationSlugMustBeValidRule(new string('a', 63)).IsBroken(), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("ab")]
    [TestCase("Acme")]
    [TestCase("acme_corp")]
    [TestCase("-acme")]
    [TestCase("acme-")]
    [TestCase("acme--corp")]
    public void Invalid_Shapes_Are_Rejected(string? slug)
    {
        Assert.That(new OrganizationSlugMustBeValidRule(slug!).IsBroken(), Is.True);
    }

    [Test]
    public void Sixty_Four_Chars_Is_Rejected()
    {
        Assert.That(new OrganizationSlugMustBeValidRule(new string('a', 64)).IsBroken(), Is.True);
    }

    [Test]
    public void Each_Reserved_Slug_Is_Rejected()
    {
        Assert.That(OrganizationSlugMustBeValidRule.Reserved, Is.Not.Empty);
        foreach (var reserved in OrganizationSlugMustBeValidRule.Reserved)
        {
            var rule = new OrganizationSlugMustBeValidRule(reserved);
            Assert.That(rule.IsBroken(), Is.True, $"reserved slug '{reserved}' must be rejected");
            Assert.That(rule.Message, Does.Contain("reserved"));
        }
    }

    [Test]
    public void Organization_Ctor_Throws_BusinessRule_On_Reserved_Slug()
    {
        Assert.Throws<BusinessRuleValidationException>(() => new Organization("Acme", "admin"));
    }
}
