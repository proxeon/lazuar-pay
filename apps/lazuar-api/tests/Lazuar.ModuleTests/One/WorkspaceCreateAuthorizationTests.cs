using System;
using System.IO;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class WorkspaceCreateAuthorizationTests
{
    [Test]
    public void Post_Workspaces_Requires_Authorization()
    {
        var source = ReadModuleFile("Infrastructure", "Endpoints", "WorkspaceEndpoints.cs");
        Assert.That(source, Does.Contain("MapPost(\"/workspaces\""));
        Assert.That(source, Does.Contain("if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();"));
        Assert.That(source, Does.Contain(".RequireAuthorization();"));
    }

    [Test]
    public void Get_Public_Pricing_Is_Anonymous()
    {
        var source = ReadModuleFile("Infrastructure", "Endpoints", "AuthEndpoints.cs");
        var pricingIndex = source.IndexOf("MapGet(\"/public/pricing\"", StringComparison.Ordinal);
        var registerIndex = source.IndexOf("MapPost(\"/public/register\"", StringComparison.Ordinal);
        Assert.That(pricingIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(registerIndex, Is.GreaterThan(pricingIndex));
        var pricingBlock = source[pricingIndex..registerIndex];
        Assert.That(pricingBlock, Does.Not.Contain("RequireAuthorization"));
    }

    private static string ReadModuleFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "Modules", "One" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            candidate = Path.Combine(new[] { dir.FullName, "apps", "lazuar-api", "Modules", "One" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        Assert.Fail($"Could not locate {string.Join("/", relativeParts)}");
        return null!;
    }
}
