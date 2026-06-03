using NUnit.Framework;
using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class ModuleBoundaryTests
{
    private static readonly Assembly TenantApplicationAssembly = typeof(Modules.Tenant.Application.DependencyInjection).Assembly;
    private static readonly Assembly MessagingApplicationAssembly = typeof(Modules.Messaging.Application.DependencyInjection).Assembly;

    private const string TenantNamespace = "Modules.Tenant";
    private const string MessagingNamespace = "Modules.Messaging";

    [Test]
    public void TenantModule_ShouldNotDependOn_MessagingModuleInternalLayers()
    {
        // Tenant can only depend on Messaging.Contracts, never Domain, Application, or Infrastructure
        var result = Types.InAssembly(TenantApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                $"{MessagingNamespace}.Domain",
                $"{MessagingNamespace}.Application",
                $"{MessagingNamespace}.Infrastructure"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Tenant module must not bypass boundaries to depend on Messaging internals.");
    }

    [Test]
    public void MessagingModule_ShouldNotDependOn_TenantModuleInternalLayers()
    {
        // Messaging can only depend on Tenant.Contracts, never Domain, Application, or Infrastructure
        var result = Types.InAssembly(MessagingApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                $"{TenantNamespace}.Domain",
                $"{TenantNamespace}.Application",
                $"{TenantNamespace}.Infrastructure"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Messaging module must not bypass boundaries to depend on Tenant internals.");
    }

    [Test]
    public void Domain_ShouldNotHave_ExternalDependencies()
    {
        var domainAssembly = typeof(BuildingBlocks.Domain.Entity).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "BuildingBlocks.Application",
                "BuildingBlocks.Infrastructure",
                "SharedKernel",
                TenantNamespace,
                MessagingNamespace
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain projects must remain completely free of application or infrastructure dependencies.");
    }
}
