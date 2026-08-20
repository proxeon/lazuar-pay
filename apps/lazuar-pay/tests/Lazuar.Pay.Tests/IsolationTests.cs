namespace Lazuar.Pay.Tests;

public class IsolationTests
{
    static readonly string[] Banned = ["lazuar-api", "Modules.", "BuildingBlocks", "MediatR", "Lazuar.Api"];

    [Test]
    public void Host_csproj_does_not_reference_the_old_api()
    {
        AssertNoBanned(File.ReadAllText(FindHostCsproj()));
    }

    [Test]
    public void Test_csproj_does_not_reference_the_old_api()
    {
        var root = FindPayRoot();
        var csproj = Path.Combine(root, "tests", "Lazuar.Pay.Tests", "Lazuar.Pay.Tests.csproj");
        Assert.That(File.Exists(csproj), Is.True);
        AssertNoBanned(File.ReadAllText(csproj));
    }

    [Test]
    public void Source_does_not_use_mediatr_or_hub_modules()
    {
        var src = Path.Combine(FindPayRoot(), "src");
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.That(text, Does.Not.Contain("MediatR"), file);
            Assert.That(text, Does.Not.Contain("Modules.One"), file);
            Assert.That(text, Does.Not.Contain("BuildingBlocks"), file);
        }
    }

    [Test]
    public void No_csproj_references_apps_lazuar_api()
    {
        var root = FindPayRoot();
        foreach (var file in Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            Assert.That(File.ReadAllText(file), Does.Not.Contain("apps/lazuar-api"), file);
            Assert.That(File.ReadAllText(file), Does.Not.Contain(@"apps\lazuar-api"), file);
        }
    }

    static void AssertNoBanned(string text)
    {
        foreach (var token in Banned)
        {
            Assert.That(text, Does.Not.Contain(token));
        }
    }

    static string FindHostCsproj() =>
        Path.Combine(FindPayRoot(), "src", "Lazuar.Pay", "Lazuar.Pay.csproj");

    static string FindPayRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "Lazuar.Pay", "Lazuar.Pay.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find apps/lazuar-pay root");
    }
}
