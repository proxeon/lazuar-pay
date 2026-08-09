using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Observability;

/// <summary>
/// Guardrail: BuildingBlocks must not hardcode product-table SQL or a module schema inventory.
/// </summary>
[TestFixture]
public class BuildingBlocksMetricsBoundaryTests
{
    private static readonly Regex ProductSqlTaxDocuments = new(
        @"FROM\s+lhdn\.""TaxDocuments""|lhdn\.""TaxDocuments""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Lazuar.slnx")) ||
                File.Exists(Path.Combine(dir.FullName, "apps", "lazuar-api", "Lazuar.slnx")))
            {
                var apiRoot = Path.Combine(dir.FullName, "apps", "lazuar-api");
                if (Directory.Exists(apiRoot))
                {
                    return apiRoot;
                }

                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", ".."));
    }

    [Test]
    public void BuildingBlocks_Sources_Contain_No_TaxDocuments_Product_SQL()
    {
        var bbRoot = Path.Combine(FindRepoRoot(), "BuildingBlocks");
        Assert.That(Directory.Exists(bbRoot), Is.True, $"Expected BuildingBlocks at {bbRoot}");

        var hits = Directory
            .EnumerateFiles(bbRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, i) => (path, line, i: i + 1))
                .Where(x =>
                {
                    var trimmed = x.line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                        trimmed.StartsWith("///", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    return ProductSqlTaxDocuments.IsMatch(x.line);
                }))
            .ToList();

        Assert.That(hits, Is.Empty,
            "BuildingBlocks must not query TaxDocuments; move product SQL to module IPlatformMetricsContributor.\n" +
            string.Join("\n", hits.Select(h => $"{h.path}:{h.i}: {h.line.Trim()}")));
    }

    [Test]
    public void PlatformMetricsCollector_Has_No_Hardcoded_ModuleSchemas_Array()
    {
        var collectorPath = Path.Combine(
            FindRepoRoot(),
            "BuildingBlocks",
            "Infrastructure",
            "Observability",
            "PlatformMetricsCollector.cs");

        Assert.That(File.Exists(collectorPath), Is.True, collectorPath);
        var source = File.ReadAllText(collectorPath);

        Assert.That(source, Does.Not.Contain("ModuleSchemas"),
            "Hardcoded ModuleSchemas inventory must be replaced by IOutboxSchemaRegistration DI.");
        Assert.That(source, Does.Not.Contain("QueryLhdnStuckAsync"),
            "LHDN stuck SQL must live in Lhdn contributor, not PlatformMetricsCollector.");
    }
}
