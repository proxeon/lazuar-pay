using NetArchTest.Rules;
using NUnit.Framework;
using System;
using System.Linq;
using Modules.Community.Domain.Aggregates;
using Modules.Payments.Domain.Aggregates;
using Modules.CRM.Domain;
using Modules.One.Domain;
using Modules.Messaging.Domain;
using Modules.Ops.Domain;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class ModuleBoundaryTests
{
    [Test]
    public void CommunityDomain_Should_Not_Reference_Other_Modules()
    {
        var result = Types.InAssembly(typeof(CommunityPlan).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Modules.CRM.Domain",
                "Modules.Payments.Domain",
                "Modules.One.Domain",
                "Modules.Messaging.Domain",
                "Modules.Ops.Domain"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, 
            $"Community Domain violates boundaries. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Test]
    public void PaymentsDomain_Should_Be_Blind_To_Community_Concepts()
    {
        var result = Types.InAssembly(typeof(TenantPaymentConfiguration).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Modules.Community.Domain")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, 
            "Payments Domain must remain completely blind to Community concepts like Coupons or Broadcasts.");
    }

    [Test]
    public void Domain_Assemblies_Should_Not_Reference_Infrastructure()
    {
        var domainAssemblies = new[]
        {
            typeof(CommunityPlan).Assembly,
            typeof(TenantPaymentConfiguration).Assembly,
            typeof(ClientProfileEntity).Assembly,
            typeof(Organization).Assembly,
            typeof(TenantReplica).Assembly,
            typeof(OpsConversation).Assembly
        };

        foreach (var assembly in domainAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "Microsoft.EntityFrameworkCore",
                    "Dapper",
                    "Npgsql",
                    "Stripe",
                    "Amazon.S3",
                    "Modules.Community.Infrastructure",
                    "Modules.Payments.Infrastructure",
                    "Modules.CRM.Infrastructure",
                    "Modules.One.Infrastructure",
                    "Modules.Messaging.Infrastructure",
                    "Modules.Ops.Infrastructure"
                )
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True, 
                $"{assembly.GetName().Name} references infrastructure or external libraries. Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }
}
