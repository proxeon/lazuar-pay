using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Lazuar.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Modules.One.Application;
using Modules.One.Domain;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class SecurityStampMiddlewareTests
{
    [Test]
    public async Task MismatchedStamp_Returns401()
    {
        var user = new GlobalUser("a@example.com", "A", "hash");
        var repo = Substitute.For<IOneRepository>();
        repo.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/one/workspaces";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("security_stamp", Guid.CreateVersion7().ToString())
            ],
            authenticationType: "Bearer"));

        var nextCalled = false;
        var middleware = new SecurityStampMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, repo);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Test]
    public async Task MatchingStamp_Continues()
    {
        var user = new GlobalUser("a@example.com", "A", "hash");
        var repo = Substitute.For<IOneRepository>();
        repo.GetUserByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/one/workspaces";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("security_stamp", user.SecurityStamp.ToString())
            ],
            authenticationType: "Bearer"));

        var nextCalled = false;
        var middleware = new SecurityStampMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, repo);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Test]
    public async Task ApiKey_SkipsStamp()
    {
        var repo = Substitute.For<IOneRepository>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/lhdn/documents";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "API_CLIENT")],
            authenticationType: "ApiKey"));

        var nextCalled = false;
        var middleware = new SecurityStampMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, repo);

        nextCalled.Should().BeTrue();
        await repo.DidNotReceive().GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
