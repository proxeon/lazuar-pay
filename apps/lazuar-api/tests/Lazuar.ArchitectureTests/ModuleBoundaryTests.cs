using System;
using System.Linq;
using System.Reflection;
using NetArchTest.Rules;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class ModuleBoundaryTests
{
    private readonly string[] _moduleNamespaces = new[]
    {
        "Modules.One",
        "Modules.Messaging",
        "Modules.Community",
        "Modules.CRM",
        "Modules.Payments",
        "Modules.Ops",
        "Modules.Billing",
        "Modules.Lhdn",
        "Modules.Commerce",
        "Modules.Vault",
        "Modules.Communications"
    };

    [Test]
    public void Domain_Should_Remain_Completely_Isolated()
    {
        foreach (var module in _moduleNamespaces)
        {
            var domainAssembly = GetAssembly($"{module}.Domain");
            if (domainAssembly == null) continue;

            // Domain must not reference its own outer layers or any part of other modules
            var forbiddenDependencies = _moduleNamespaces
                .Where(m => m != module)
                .Concat(new[] { $"{module}.Infrastructure", $"{module}.Application" })
                .ToArray();

            var result = Types.InAssembly(domainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenDependencies)
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True, $"Domain layer in {module} has invalid outer or cross-module dependencies.");
        }
    }

    [Test]
    public void Application_Should_Not_Reference_Infrastructure()
    {
        foreach (var module in _moduleNamespaces)
        {
            var appAssembly = GetAssembly($"{module}.Application");
            if (appAssembly == null) continue;

            var result = Types.InAssembly(appAssembly)
                .ShouldNot()
                .HaveDependencyOn($"{module}.Infrastructure")
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True, $"Application layer in {module} incorrectly references its own Infrastructure.");
        }
    }

    [Test]
    public void Outer_Layers_Should_Only_Reference_Other_Modules_Through_Contracts()
    {
        foreach (var module in _moduleNamespaces)
        {
            var appAssembly = GetAssembly($"{module}.Application");
            var infraAssembly = GetAssembly($"{module}.Infrastructure");

            // Exclude public .Contracts namespaces from the forbidden list
            var forbiddenNamespaces = _moduleNamespaces
                .Where(m => m != module)
                .SelectMany(m => new[] { $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure" })
                .ToArray();

            if (appAssembly != null)
            {
                var appResult = Types.InAssembly(appAssembly)
                    .ShouldNot()
                    .HaveDependencyOnAny(forbiddenNamespaces)
                    .GetResult();

                Assert.That(appResult.IsSuccessful, Is.True, $"Application layer in {module} bypasses Contracts to reference other modules directly.");
            }

            if (infraAssembly != null)
            {
                var infraResult = Types.InAssembly(infraAssembly)
                    .ShouldNot()
                    .HaveDependencyOnAny(forbiddenNamespaces)
                    .GetResult();

                Assert.That(infraResult.IsSuccessful, Is.True, $"Infrastructure layer in {module} (including DbContext) bypasses Contracts to reference other modules directly.");
            }
        }
    }

    private static Assembly? GetAssembly(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName);
    }
}
