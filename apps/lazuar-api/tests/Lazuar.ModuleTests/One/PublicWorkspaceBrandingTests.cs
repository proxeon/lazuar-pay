using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.Services;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class PublicWorkspaceBrandingTests
{
    [Test]
    public async Task Unknown_And_Inactive_Are_Null()
    {
        await using var db = CreateDb();
        var inactive = new Organization("Gone", "gone");
        inactive.Archive();
        db.Organizations.Add(inactive);
        await db.SaveChangesAsync();

        var svc = new OneQueryService(db, Substitute.For<ISecretVault>());
        (await svc.GetPublicBrandingBySlugAsync("missing")).Should().BeNull();
        (await svc.GetPublicBrandingBySlugAsync("gone")).Should().BeNull();
    }

    [Test]
    public async Task Active_Returns_Name_Slug_And_Optional_Brand()
    {
        await using var db = CreateDb();
        var org = new Organization("Acme Co", "acme");
        org.UpdateBranding("https://cdn.example/logo.png", "#112233");
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var svc = new OneQueryService(db, Substitute.For<ISecretVault>());
        var branding = await svc.GetPublicBrandingBySlugAsync("acme");
        branding.Should().NotBeNull();
        branding!.Name.Should().Be("Acme Co");
        branding.Slug.Should().Be("acme");
        branding.LogoUrl.Should().Be("https://cdn.example/logo.png");
        branding.PrimaryColor.Should().Be("#112233");
    }

    private static OneDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new FakeExecutionContextAccessor { TenantId = Guid.Empty, UserId = Guid.Empty };
        return new OneDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());
    }
}
