using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetArchTest.Rules;
using NUnit.Framework;

namespace Lazuar.ArchitectureTests;

[TestFixture]
public class ModuleBoundaryTests
{
    // Anchors for BuildingBlocks / SharedKernel (C.9 architecture expansion).
    private static readonly Assembly BuildingBlocksDomainAssembly =
        typeof(BuildingBlocks.Domain.Entity).Assembly;
    private static readonly Assembly BuildingBlocksApplicationAssembly =
        typeof(BuildingBlocks.Application.ICommand).Assembly;
    private static readonly Assembly BuildingBlocksInfrastructureAssembly =
        typeof(BuildingBlocks.Infrastructure.PlatformDbContext).Assembly;
    private static readonly Assembly SharedKernelAssembly =
        typeof(SharedKernel.SharedKernelMarker).Assembly;

    private static readonly string[] ModuleNamespaces =
    [
        "Modules.One",
        "Modules.Messaging",
        "Modules.CRM",
        "Modules.Payments",
        "Modules.Ops",
        "Modules.Billing",
        "Modules.Lhdn",
        "Modules.Commerce",
        "Modules.Communications"
    ];

    /// <summary>
    /// Modules intentionally without an Application layer (Infrastructure hosts handlers/ports).
    /// </summary>
    private static readonly HashSet<string> ModulesWithoutApplication = new(StringComparer.Ordinal)
    {
        "Modules.CRM"
    };

    /// <summary>
    /// Assemblies forced loaded via typeof anchors so NetArchTest can inspect them.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Assembly> LoadedModuleAssemblies;

    static ModuleBoundaryTests()
    {
        // Force-load module assemblies. ProjectReferences alone do not load into AppDomain
        // until a type is touched; NetArchTest requires the assemblies to be present.
        Assembly[] anchors =
        [
            typeof(Modules.One.Domain.GlobalUser).Assembly,
            typeof(Modules.One.Application.DependencyInjection).Assembly,
            typeof(Modules.One.Infrastructure.DependencyInjection).Assembly,

            typeof(Modules.Messaging.Domain.TenantReplica).Assembly,
            typeof(Modules.Messaging.Application.DependencyInjection).Assembly,
            typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly,

            typeof(Modules.CRM.Domain.ClientProfileEntity).Assembly,
            typeof(Modules.CRM.Infrastructure.DependencyInjection).Assembly,

            typeof(Modules.Payments.Domain.Aggregates.TenantPaymentConfiguration).Assembly,
            typeof(Modules.Payments.Application.DependencyInjection).Assembly,
            typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly,

            typeof(Modules.Ops.Domain.OpsConversation).Assembly,
            typeof(Modules.Ops.Application.DependencyInjection).Assembly,
            typeof(Modules.Ops.Infrastructure.DependencyInjection).Assembly,

            typeof(Modules.Billing.Domain.Entities.LedgerLine).Assembly,
            typeof(Modules.Billing.Application.DependencyInjection).Assembly,
            typeof(Modules.Billing.Infrastructure.DependencyInjection).Assembly,

            typeof(Modules.Lhdn.Domain.Entities.CountryCode).Assembly,
            typeof(Modules.Lhdn.Application.DependencyInjection).Assembly,
            typeof(Modules.Lhdn.Infrastructure.DependencyInjection).Assembly,

            typeof(Modules.Commerce.Domain.Entities.DunningStep).Assembly,
            typeof(Modules.Commerce.Application.DependencyInjection).Assembly,
            typeof(Modules.Commerce.Infrastructure.DependencyInjection).Assembly,

            typeof(Modules.Communications.Domain.Aggregates.Broadcast).Assembly,
            typeof(Modules.Communications.Application.DependencyInjection).Assembly,
            typeof(Modules.Communications.Infrastructure.DependencyInjection).Assembly
        ];

        LoadedModuleAssemblies = anchors
            .GroupBy(a => a.GetName().Name ?? string.Empty, StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    [Test]
    public void Domain_Should_Remain_Completely_Isolated()
    {
        foreach (var module in ModuleNamespaces)
        {
            var domainAssembly = GetRequiredAssembly($"{module}.Domain");

            // Domain must not reference its own outer layers or any part of other modules
            var forbiddenDependencies = ModuleNamespaces
                .Where(m => m != module)
                .Concat([$"{module}.Infrastructure", $"{module}.Application"])
                .ToArray();

            var result = Types.InAssembly(domainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenDependencies)
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True,
                $"Domain layer in {module} has invalid outer or cross-module dependencies. " +
                $"Failing types: {FormatFailingTypes(result)}");
        }
    }

    [Test]
    public void Application_Should_Not_Reference_Infrastructure()
    {
        foreach (var module in ModuleNamespaces)
        {
            if (ModulesWithoutApplication.Contains(module))
            {
                continue;
            }

            var appAssembly = GetRequiredAssembly($"{module}.Application");

            var result = Types.InAssembly(appAssembly)
                .ShouldNot()
                .HaveDependencyOn($"{module}.Infrastructure")
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True,
                $"Application layer in {module} incorrectly references its own Infrastructure. " +
                $"Failing types: {FormatFailingTypes(result)}");
        }
    }

    [Test]
    public void Outer_Layers_Should_Only_Reference_Other_Modules_Through_Contracts()
    {
        foreach (var module in ModuleNamespaces)
        {
            // Exclude public .Contracts namespaces from the forbidden list
            var forbiddenNamespaces = ModuleNamespaces
                .Where(m => m != module)
                .SelectMany(m => new[] { $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure" })
                .ToArray();

            if (!ModulesWithoutApplication.Contains(module))
            {
                var appAssembly = GetRequiredAssembly($"{module}.Application");
                var appResult = Types.InAssembly(appAssembly)
                    .ShouldNot()
                    .HaveDependencyOnAny(forbiddenNamespaces)
                    .GetResult();

                Assert.That(appResult.IsSuccessful, Is.True,
                    $"Application layer in {module} bypasses Contracts to reference other modules directly. " +
                    $"Failing types: {FormatFailingTypes(appResult)}");
            }

            var infraAssembly = GetRequiredAssembly($"{module}.Infrastructure");
            var infraResult = Types.InAssembly(infraAssembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenNamespaces)
                .GetResult();

            Assert.That(infraResult.IsSuccessful, Is.True,
                $"Infrastructure layer in {module} (including DbContext) bypasses Contracts to reference other modules directly. " +
                $"Failing types: {FormatFailingTypes(infraResult)}");
        }
    }

    [Test]
    public void All_Modules_Should_Have_OutboxPublisherJob_In_Infrastructure()
    {
        foreach (var module in ModuleNamespaces)
        {
            var infraAssembly = GetRequiredAssembly($"{module}.Infrastructure");

            var jobTypes = infraAssembly.GetTypes()
                .Where(t =>
                    !t.IsAbstract &&
                    t.Name.EndsWith("OutboxPublisherJob", StringComparison.Ordinal))
                .ToList();

            Assert.That(jobTypes, Is.Not.Empty,
                $"{module} Infrastructure must define a concrete *OutboxPublisherJob type " +
                $"(modules that use OutboxEventBus must publish).");
        }
    }

    private static Assembly GetRequiredAssembly(string assemblyName)
    {
        if (LoadedModuleAssemblies.TryGetValue(assemblyName, out var anchored))
        {
            return anchored;
        }

        var fromDomain = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName);
        if (fromDomain is not null)
        {
            return fromDomain;
        }

        try
        {
            return Assembly.Load(new AssemblyName(assemblyName));
        }
        catch (Exception ex)
        {
            Assert.Fail(
                $"Expected assembly '{assemblyName}' was not loaded. " +
                "Add a ProjectReference in Lazuar.ArchitectureTests.csproj and a typeof(...) anchor " +
                $"in ModuleBoundaryTests static constructor. Load error: {ex.Message}");
            throw;
        }
    }

    private static string FormatFailingTypes(TestResult result)
    {
        if (result.FailingTypeNames is null || result.FailingTypeNames.Count == 0)
        {
            return "(none reported)";
        }

        return string.Join(", ", result.FailingTypeNames);
    }

    /// <summary>
    /// C.9 — BuildingBlocks must remain free of module business assemblies (domain-agnostic technical core).
    /// </summary>
    [Test]
    public void BuildingBlocks_Must_Not_Reference_Module_Assemblies()
    {
        var forbidden = ModuleNamespaces
            .SelectMany(m => new[] { m, $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure", $"{m}.Contracts" })
            .Distinct()
            .ToArray();

        foreach (var assembly in new[]
                 {
                     BuildingBlocksDomainAssembly,
                     BuildingBlocksApplicationAssembly,
                     BuildingBlocksInfrastructureAssembly
                 })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True,
                $"BuildingBlocks assembly '{assembly.GetName().Name}' must not reference Modules.*. " +
                $"Failing types: {FormatFailingTypes(result)}");
        }
    }

    /// <summary>
    /// C.9 — SharedKernel stays free of write-model business entities and module layers.
    /// </summary>
    [Test]
    public void SharedKernel_Must_Not_Reference_Modules_Or_Contain_Entity_Types()
    {
        var forbidden = ModuleNamespaces
            .SelectMany(m => new[] { m, $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure", $"{m}.Contracts" })
            .Concat(["BuildingBlocks.Application", "BuildingBlocks.Infrastructure"])
            .Distinct()
            .ToArray();

        var result = Types.InAssembly(SharedKernelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"SharedKernel must not reference Modules.* or BuildingBlocks Application/Infrastructure. " +
            $"Failing types: {FormatFailingTypes(result)}");

        // No Entity / AggregateRoot subclasses — SharedKernel is marker / pure types only.
        var entitySubtypes = SharedKernelAssembly.GetTypes()
            .Where(t => !t.IsAbstract
                        && (typeof(BuildingBlocks.Domain.Entity).IsAssignableFrom(t)
                            || typeof(BuildingBlocks.Domain.IAggregateRoot).IsAssignableFrom(t)))
            .Select(t => t.FullName)
            .ToList();

        Assert.That(entitySubtypes, Is.Empty,
            "SharedKernel must not host Entity/IAggregateRoot write models: " + string.Join(", ", entitySubtypes));
    }

    /// <summary>
    /// C.9 — Module Domain layers may depend on BuildingBlocks.Domain only (not Application/Infrastructure).
    /// </summary>
    [Test]
    public void Module_Domain_Should_Not_Reference_BuildingBlocks_Application_Or_Infrastructure()
    {
        foreach (var module in ModuleNamespaces)
        {
            var domainAssembly = GetRequiredAssembly($"{module}.Domain");

            var result = Types.InAssembly(domainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny("BuildingBlocks.Application", "BuildingBlocks.Infrastructure")
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True,
                $"Domain layer in {module} must not reference BuildingBlocks.Application or BuildingBlocks.Infrastructure. " +
                $"Failing types: {FormatFailingTypes(result)}");
        }
    }

    /// <summary>
    /// Phase 17.5 — Host composes modules via Infrastructure entrypoints only.
    /// Application assemblies stay transitive (Infrastructure → Application) for MediatR markers;
    /// do not re-add direct <c>*Application.csproj</c> ProjectReferences on the host.
    /// </summary>
    [Test]
    public void Host_Csproj_Must_Not_Directly_Reference_Module_Application_Projects()
    {
        var hostCsproj = FindHostCsprojPath();
        Assert.That(File.Exists(hostCsproj), Is.True, $"Host csproj not found at '{hostCsproj}'.");

        var text = File.ReadAllText(hostCsproj);
        var forbidden = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.Contains("ProjectReference", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Match module Application projects only (not BuildingBlocks.Application if ever added).
            if (trimmed.Contains("Modules.", StringComparison.Ordinal)
                && trimmed.Contains("Application", StringComparison.Ordinal)
                && trimmed.Contains(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                forbidden.Add(trimmed);
            }
        }

        Assert.That(forbidden, Is.Empty,
            "Lazuar.Api.csproj must not ProjectReference Modules.*.Application. " +
            "Compose via Infrastructure only; Application is transitive. Offending lines:\n" +
            string.Join("\n", forbidden));
    }

    private static string FindHostCsprojPath()
    {
        // tests/Lazuar.ArchitectureTests → apps/lazuar-api/src/Lazuar.Api/Lazuar.Api.csproj
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Lazuar.Api", "Lazuar.Api.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Also walk from repo-relative paths when TestDirectory is bin/Debug/netX
            var sibling = Path.Combine(dir.FullName, "Lazuar.Api.csproj");
            if (File.Exists(sibling) && dir.Name == "Lazuar.Api")
            {
                return sibling;
            }

            dir = dir.Parent;
        }

        // Fallback: relative from ArchitectureTests project folder layout
        return Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "src", "Lazuar.Api", "Lazuar.Api.csproj"));
    }
}
