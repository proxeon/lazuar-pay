namespace Lazuar.Pay.Tests;

public class IsolationTests
{
    [Test]
    public void Host_csproj_does_not_reference_the_old_api()
    {
        var csproj = FindHostCsproj();
        var text = File.ReadAllText(csproj);

        Assert.That(text, Does.Not.Contain("lazuar-api"));
        Assert.That(text, Does.Not.Contain("Modules."));
        Assert.That(text, Does.Not.Contain("BuildingBlocks"));
        Assert.That(text, Does.Not.Contain("MediatR"));
        Assert.That(text, Does.Not.Contain("Lazuar.Api"));
    }

    static string FindHostCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Lazuar.Pay", "Lazuar.Pay.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find src/Lazuar.Pay/Lazuar.Pay.csproj");
    }
}
