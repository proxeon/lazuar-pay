namespace Lazuar.Pay.Tests;

public class IsolationTests
{
    static readonly string[] Banned = ["lazuar-api", "Modules.", "BuildingBlocks", "MediatR", "Lazuar.Api"];
    static readonly string[] BannedSrc =
    [
        "MediatR", "Modules.One", "BuildingBlocks", "IPaymentGatewayAdapter", "PaymentGatewayFactory",
        "IPaymentGatewayFactory", "AddPaymentsModule", "GatewayPaymentCompletedIntegrationEvent", "Modules.Payments",
        "ApplicationFeeAmount", "Razorpay.Api",
        "application_fee", "TransferData", "transfer_data",
        "ChipWebhookRegistrar", "PublicDnsFallback",
        "Lhdn", "MyInvois", "UBL", "XAdES", "Irbm"
    ];

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
            foreach (var token in BannedSrc)
            {
                Assert.That(text, Does.Not.Contain(token), file);
            }
        }
    }

    [Test]
    public void Source_does_not_create_org_or_user_tables()
    {
        var src = Path.Combine(FindPayRoot(), "src");
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.That(text, Does.Not.Contain("ToTable(\"organizations\")"), file);
            Assert.That(text, Does.Not.Contain("ToTable(\"users\")"), file);
            Assert.That(text, Does.Not.Contain("ToTable(\"members\")"), file);
        }
    }

    [Test]
    public void Vite_apps_do_not_use_hub_types()
    {
        var repo = FindPayRoot();
        while (repo is not null && !Directory.Exists(Path.Combine(repo, "apps", "lazuar-pay-merchant")))
        {
            repo = Directory.GetParent(repo)?.FullName;
        }

        Assert.That(repo, Is.Not.Null);
        foreach (var name in new[] { "lazuar-pay-merchant", "lazuar-pay-checkout" })
        {
            var pkg = Path.Combine(repo, "apps", name, "package.json");
            Assert.That(File.Exists(pkg), Is.True, pkg);
            var text = File.ReadAllText(pkg);
            Assert.That(text, Does.Not.Contain("@repo/api-types-ts"), pkg);
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
            Assert.That(File.ReadAllText(file), Does.Not.Contain("Razorpay.Api"), file);
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
