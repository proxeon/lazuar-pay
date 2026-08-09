using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.One.Contracts;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.Services;
using Modules.Payments.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class PlatformAdminAuthQueryTests
{
    [Test]
    public async Task GetSystemAdminByEmail_Returns_Login_Dto_With_PasswordHash()
    {
        await using var db = CreateDb();
        var admin = new GlobalUser("admin@lazuar.com", "Platform Admin", "hash-secret", isSystemAdmin: true, isEmailVerified: true);
        var member = new GlobalUser("member@lazuar.com", "Member", "hash-other", isSystemAdmin: false);
        db.GlobalUsers.AddRange(admin, member);
        await db.SaveChangesAsync();

        var query = new PlatformAdminAuthQuery(db);
        var found = await query.GetSystemAdminByEmailAsync("  Admin@Lazuar.com ");

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Id, Is.EqualTo(admin.Id));
        Assert.That(found.Email, Is.EqualTo("admin@lazuar.com"));
        Assert.That(found.Name, Is.EqualTo("Platform Admin"));
        Assert.That(found.PasswordHash, Is.EqualTo("hash-secret"));
        Assert.That(found.SecurityStamp, Is.EqualTo(admin.SecurityStamp));
        Assert.That(found.IsSystemAdmin, Is.True);
        Assert.That(found.IsActive, Is.True);
        Assert.That(found.IsEmailVerified, Is.True);
    }

    [Test]
    public async Task GetSystemAdminByEmail_Returns_Null_For_Non_System_Admin()
    {
        await using var db = CreateDb();
        db.GlobalUsers.Add(new GlobalUser("user@example.com", "User", "hash", isSystemAdmin: false));
        await db.SaveChangesAsync();

        var query = new PlatformAdminAuthQuery(db);
        var found = await query.GetSystemAdminByEmailAsync("user@example.com");

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task GetSystemAdminById_Returns_Dto_Without_PasswordHash_Property()
    {
        await using var db = CreateDb();
        var admin = new GlobalUser("admin@lazuar.com", "Admin", "hash-secret", isSystemAdmin: true);
        db.GlobalUsers.Add(admin);
        await db.SaveChangesAsync();

        var query = new PlatformAdminAuthQuery(db);
        var found = await query.GetSystemAdminByIdAsync(admin.Id);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Id, Is.EqualTo(admin.Id));
        Assert.That(found.Email, Is.EqualTo("admin@lazuar.com"));
        Assert.That(found.SecurityStamp, Is.EqualTo(admin.SecurityStamp));
        Assert.That(found.IsSystemAdmin, Is.True);

        // /me projection must not expose password hash on the type.
        Assert.That(typeof(PlatformAdminUserDto).GetProperty("PasswordHash"), Is.Null);
    }

    [Test]
    public async Task GetSystemAdminById_Returns_Null_When_Not_System_Admin()
    {
        await using var db = CreateDb();
        var user = new GlobalUser("user@example.com", "User", "hash", isSystemAdmin: false);
        db.GlobalUsers.Add(user);
        await db.SaveChangesAsync();

        var query = new PlatformAdminAuthQuery(db);
        var found = await query.GetSystemAdminByIdAsync(user.Id);

        Assert.That(found, Is.Null);
    }

    [Test]
    public void MapPlatformAuthEndpoints_LoginAndLogout_AllowAnonymous_Me_RequiresAuth()
    {
        var endpoints = MapPlatformAuthRoutes();

        var login = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("auth/login", StringComparison.Ordinal)
            && HasMethod(e, "POST"));
        var logout = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("auth/logout", StringComparison.Ordinal)
            && HasMethod(e, "POST"));
        var me = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("auth/me", StringComparison.Ordinal)
            && HasMethod(e, "GET"));

        Assert.That(login, Is.Not.Null, "POST /auth/login missing");
        Assert.That(logout, Is.Not.Null, "POST /auth/logout missing");
        Assert.That(me, Is.Not.Null, "GET /auth/me missing");

        Assert.That(login!.Metadata.GetMetadata<IAllowAnonymous>(), Is.Not.Null, "login should AllowAnonymous");
        Assert.That(logout!.Metadata.GetMetadata<IAllowAnonymous>(), Is.Not.Null, "logout should AllowAnonymous");
        // /me inherits host SUPER_ADMIN group; no AllowAnonymous override.
        Assert.That(me!.Metadata.GetMetadata<IAllowAnonymous>(), Is.Null, "me must not be anonymous");
    }

    [Test]
    public void MapPlatformEndpoints_Payments_Only_Maps_PaymentConfig_Not_Auth()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<IExecutionContextAccessor>());
        var app = builder.Build();
        app.MapGroup("/api/v1/platform").MapPlatformEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        Assert.That(endpoints.Any(e =>
            e.RoutePattern.RawText is { } raw && raw.Contains("payment-config", StringComparison.Ordinal)), Is.True);
        Assert.That(endpoints.Any(e =>
            e.RoutePattern.RawText is { } raw && raw.Contains("auth/", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void Payments_Infrastructure_Has_No_one_Schema_Sql()
    {
        var paymentsInfra = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "Modules", "Payments", "Infrastructure"));

        Assert.That(Directory.Exists(paymentsInfra), Is.True, $"Missing path: {paymentsInfra}");

        var offenders = Directory.EnumerateFiles(paymentsInfra, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.Contains("one.\"", StringComparison.Ordinal)
                       || text.Contains("one.GlobalUsers", StringComparison.Ordinal)
                       || text.Contains("FROM one.", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("JOIN one.", StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => Path.GetRelativePath(paymentsInfra, path))
            .ToList();

        Assert.That(offenders, Is.Empty,
            "Payments must not embed one.* SQL (L-02). Offenders: " + string.Join(", ", offenders));
    }

    private static System.Collections.Generic.List<RouteEndpoint> MapPlatformAuthRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IPlatformAdminAuthQuery>());
        builder.Services.AddSingleton(Substitute.For<IPasswordService>());
        builder.Services.AddSingleton(Substitute.For<IJwtService>());

        var app = builder.Build();
        app.MapGroup("/api/v1/platform")
            .RequireAuthorization(policy => policy.RequireRole("SUPER_ADMIN"))
            .MapPlatformAuthEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static bool HasMethod(RouteEndpoint e, string method) =>
        e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
            .Any(m => string.Equals(m, method, StringComparison.OrdinalIgnoreCase)) == true;

    private static OneDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new FakeExecutionContextAccessor { TenantId = Guid.Empty, UserId = Guid.Empty };
        return new OneDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());
    }
}
