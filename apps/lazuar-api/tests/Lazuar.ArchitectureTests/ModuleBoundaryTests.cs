using NUnit.Framework;
using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Infrastructure;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class ModuleBoundaryTests
{
    private static readonly Assembly TenantApplicationAssembly = typeof(Modules.Tenant.Application.DependencyInjection).Assembly;
    private static readonly Assembly MessagingApplicationAssembly = typeof(Modules.Messaging.Application.DependencyInjection).Assembly;
    private static readonly Assembly CommunityApplicationAssembly = typeof(Modules.Community.Application.DependencyInjection).Assembly;
    private static readonly Assembly PaymentsApplicationAssembly = typeof(Modules.Payments.Application.DependencyInjection).Assembly;

    // Infrastructure assembly definitions for DbContext architecture checks
    private static readonly Assembly TenantInfrastructureAssembly = typeof(Modules.Tenant.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly MessagingInfrastructureAssembly = typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly CommunityInfrastructureAssembly = typeof(Modules.Community.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly CrmInfrastructureAssembly = typeof(Modules.CRM.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly PaymentsInfrastructureAssembly = typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly;

    private const string TenantNamespace = "Modules.Tenant";
    private const string MessagingNamespace = "Modules.Messaging";
    private const string CommunityNamespace = "Modules.Community";
    private const string UserAccessNamespace = "Modules.UserAccess";
    private const string CrmNamespace = "Modules.CRM";
    private const string PaymentsNamespace = "Modules.Payments";

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
    public void CommunityModule_ShouldNotDependOn_OtherModulesInternalLayers()
    {
        var result = Types.InAssembly(CommunityApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                $"{TenantNamespace}.Domain",
                $"{TenantNamespace}.Application",
                $"{TenantNamespace}.Infrastructure",
                $"{MessagingNamespace}.Domain",
                $"{MessagingNamespace}.Application",
                $"{MessagingNamespace}.Infrastructure",
                $"{CrmNamespace}.Domain",
                $"{CrmNamespace}.Application",
                $"{CrmNamespace}.Infrastructure",
                $"{PaymentsNamespace}.Domain",
                $"{PaymentsNamespace}.Application",
                $"{PaymentsNamespace}.Infrastructure"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Community module must not bypass boundaries to depend on internal layers of other modules.");
    }

    [Test]
    public void PaymentsModule_ShouldNotDependOn_OtherModulesInternalLayers()
    {
        var result = Types.InAssembly(PaymentsApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                $"{TenantNamespace}.Domain",
                $"{TenantNamespace}.Application",
                $"{TenantNamespace}.Infrastructure",
                $"{MessagingNamespace}.Domain",
                $"{MessagingNamespace}.Application",
                $"{MessagingNamespace}.Infrastructure",
                $"{CommunityNamespace}.Domain",
                $"{CommunityNamespace}.Application",
                $"{CommunityNamespace}.Infrastructure",
                $"{CrmNamespace}.Domain",
                $"{CrmNamespace}.Application",
                $"{CrmNamespace}.Infrastructure",
                $"{UserAccessNamespace}.Domain",
                $"{UserAccessNamespace}.Application",
                $"{UserAccessNamespace}.Infrastructure"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue("Payments module must not bypass boundaries to depend on internal layers of other modules.");
    }

    [Test]
    public void CrmModule_ShouldNotDependOn_OtherModulesInternalLayers()
    {
        var result = Types.InAssembly(CrmInfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                $"{TenantNamespace}.Domain",
                $"{TenantNamespace}.Application",
                $"{TenantNamespace}.Infrastructure",
                $"{MessagingNamespace}.Domain",
                $"{MessagingNamespace}.Application",
                $"{MessagingNamespace}.Infrastructure",
                $"{CommunityNamespace}.Domain",
                $"{CommunityNamespace}.Application",
                $"{CommunityNamespace}.Infrastructure",
                $"{PaymentsNamespace}.Domain",
                $"{PaymentsNamespace}.Application",
                $"{PaymentsNamespace}.Infrastructure",
                $"{UserAccessNamespace}.Domain",
                $"{UserAccessNamespace}.Application",
                $"{UserAccessNamespace}.Infrastructure"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue("CRM module must not bypass boundaries to depend on internal layers of other modules.");
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
                CommunityNamespace,
                UserAccessNamespace,
                CrmNamespace,
                PaymentsNamespace
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

    [Test]
    public void DatabaseContextClasses_ShouldResideInInfrastructure_OrBeMarkedInternal()
    {
        var assemblies = new[]
        {
            TenantInfrastructureAssembly,
            MessagingInfrastructureAssembly,
            CommunityInfrastructureAssembly,
            CrmInfrastructureAssembly,
            PaymentsInfrastructureAssembly
        };

        var dbContextTypes = Types.InAssemblies(assemblies)
            .That()
            .Inherit(typeof(DbContext))
            .Or()
            .Inherit(typeof(PlatformDbContext));

        var result = dbContextTypes
            .Should()
            .NotBePublic() // Corrected NetArchTest condition for non-public access verification
            .Or()
            .ResideInNamespaceEndingWith("Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "To prevent connection and transaction leaks, all DbContext classes must remain internal or reside strictly within their module's Infrastructure namespace.");
    }
}
