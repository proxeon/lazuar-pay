using FluentAssertions;
using Modules.Commerce.Application;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class SstTaxMathTests
{
    [Test]
    public void Product06_NoTax()
    {
        var (type, tax) = SstTaxMath.Compute("06", 8, 100m, merchantHasSstRegistration: true);
        type.Should().Be("06");
        tax.Should().Be(0m);
    }

    [Test]
    public void Product02_WithSstId_ComputesExclusiveTax()
    {
        var (type, tax) = SstTaxMath.Compute("02", 8, 100m, merchantHasSstRegistration: true);
        type.Should().Be("02");
        tax.Should().Be(8m);
    }

    [Test]
    public void Product02_WithoutSstId_Coerces06()
    {
        var (type, tax) = SstTaxMath.Compute("02", 8, 100m, merchantHasSstRegistration: false);
        type.Should().Be("06");
        tax.Should().Be(0m);
    }
}
