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
    private const string UserAccessNamespace = "Modules.UserAccess";
    private const string CrmNamespace = "Modules.CRM";

    [Test]
    public void TenantModule_ShouldNotDependOn_MessagingModuleInternalLayers()
    {
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
                MessagingNamespace,
                UserAccessNamespace,
                CrmNamespace
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain projects must remain completely free of application or infrastructure dependencies.");
    }

    [Test]
    public void SharedKernel_ShouldNotContain_DomainEntitiesOrAggregateRoots()
    {
        var sharedKernelAssembly = typeof(SharedKernel.SharedKernelMarker).Assembly;

        var failingTypes = Types.InAssembly(sharedKernelAssembly)
            .That()
            .Inherit(typeof(BuildingBlocks.Domain.Entity))
            .Or()
            .ImplementInterface(typeof(BuildingBlocks.Domain.IAggregateRoot))
            .GetTypes();

        failingTypes.Should().BeEmpty("SharedKernel must remain strictly domain-agnostic and contain zero entities or aggregate roots.");
    }

    [Test]
    public void DomainEvents_ShouldNotInherit_IntegrationEvents()
    {
        var domainAssembly = typeof(BuildingBlocks.Domain.Entity).Assembly;
        
        var result = Types.InAssembly(domainAssembly)
            .That()
            .ImplementInterface(typeof(BuildingBlocks.Domain.IDomainEvent))
            .Should()
            .NotImplementInterface(typeof(BuildingBlocks.Application.IIntegrationEvent))
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Domain events must be strictly internal and not double as Integration events.");
    }
}
