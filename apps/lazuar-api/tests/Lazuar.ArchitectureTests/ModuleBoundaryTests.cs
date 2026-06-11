using NetArchTest.Rules;
using NUnit.Framework;
using System;
using System.Linq;

namespace Lazuar.ArchitectureTests;

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
        "Modules.Billing"
    };

    [Test]
    public void Domain_Should_Not_Reference_Infrastructure_Or_Application()
    {
        foreach (var module in _moduleNamespaces)
        {
            var domainAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.FullName?.Contains($"{module}.Domain") == true);

            if (domainAssembly == null) continue;

            var result = Types.InAssembly(domainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny($"{module}.Infrastructure", $"{module}.Application")
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True, $"Domain layer in {module} violates boundaries.");
        }
    }

    [Test]
    public void Application_Should_Not_Reference_Infrastructure()
    {
        foreach (var module in _moduleNamespaces)
        {
            var appAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.FullName?.Contains($"{module}.Application") == true);

            if (appAssembly == null) continue;

            var result = Types.InAssembly(appAssembly)
                .ShouldNot()
                .HaveDependencyOn($"{module}.Infrastructure")
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True, $"Application layer in {module} violates boundaries.");
        }
    }

    [Test]
    public void Modules_Should_Not_Reference_Other_Modules_Directly()
    {
        foreach (var module in _moduleNamespaces)
        {
            var domainAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.FullName?.Contains($"{module}.Domain") == true);
            
            var appAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.FullName?.Contains($"{module}.Application") == true);

            var otherModules = _moduleNamespaces.Where(m => m != module).ToArray();

            if (domainAssembly != null)
            {
                var domainResult = Types.InAssembly(domainAssembly)
                    .ShouldNot()
                    .HaveDependencyOnAny(otherModules)
                    .GetResult();
                    
                Assert.That(domainResult.IsSuccessful, Is.True, $"Domain in {module} references another module.");
            }
        }
    }
}
