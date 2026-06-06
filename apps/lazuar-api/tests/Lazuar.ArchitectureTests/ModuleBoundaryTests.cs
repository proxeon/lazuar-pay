using FluentAssertions;
using NetArchTest.Rules;
using NUnit.Framework;
using System.Reflection;

namespace Lazuar.ArchitectureTests;

public class ModuleBoundaryTests
{
    private static readonly Assembly[] ModuleAssemblies =
    {
        typeof(Modules.Community.Application.DependencyInjection).Assembly,
        typeof(Modules.Messaging.Application.DependencyInjection).Assembly,
        typeof(Modules.Payments.Application.DependencyInjection).Assembly,
        typeof(Modules.One.Application.DependencyInjection).Assembly,
        typeof(Modules.CRM.Contracts.CreateClientProfileCommand).Assembly 
    };

    private static readonly string[] ModuleNamespaces =
    {
        "Modules.Community",
        "Modules.Messaging",
        "Modules.Payments",
        "Modules.One",
        "Modules.CRM"
    };

    [Test]
    public void Modules_ShouldNotHave_CrossModuleDependencies_OutsideContracts()
    {
        foreach (var moduleNamespace in ModuleNamespaces)
        {
            var otherModules = ModuleNamespaces
                .Where(m => m != moduleNamespace)
                .ToList();

            // A module can ONLY reference the .Contracts namespace of another module.
            // It MUST NOT reference .Application, .Domain, or .Infrastructure of other modules.
            var forbiddenNamespaces = otherModules.SelectMany(m => new[]
            {
                $"{m}.Application",
                $"{m}.Domain",
                $"{m}.Infrastructure"
            }).ToArray();

            var result = Types.InAssemblies(ModuleAssemblies)
                .That().ResideInNamespace(moduleNamespace)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenNamespaces)
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"Module {moduleNamespace} violates strict isolation boundaries. It is directly referencing the internals of another module.");
        }
    }

    [Test]
    public void SharedKernel_ShouldNotHave_DependencyOn_Modules()
    {
        // SharedKernel must be pure and completely domain-agnostic.
        var result = Types.InAssembly(typeof(SharedKernel.SharedKernelMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ModuleNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue("SharedKernel must not reference any specific module.");
    }
}
