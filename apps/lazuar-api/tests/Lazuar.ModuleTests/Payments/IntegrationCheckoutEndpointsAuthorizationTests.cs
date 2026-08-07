using System.Linq;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Payments.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

/// <summary>
/// Ensures M2M checkout routes bind the Phase 1 policies (no anonymous / wrong surface).
/// </summary>
[TestFixture]
public class IntegrationCheckoutEndpointsAuthorizationTests
{
    private static System.Collections.Generic.List<RouteEndpoint> MapIntegrationRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<IExecutionContextAccessor>());

        var app = builder.Build();
        app.MapPaymentsIntegrationEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static bool HasMethod(RouteEndpoint e, string method) =>
        e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
            .Any(m => string.Equals(m, method, System.StringComparison.OrdinalIgnoreCase)) == true;

    [Test]
    public void MapPaymentsIntegrationEndpoints_PostRequiresWritePolicy()
    {
        var endpoints = MapIntegrationRoutes();
        var post = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("integrations/payments/checkouts", System.StringComparison.Ordinal)
            && HasMethod(e, "POST"));

        Assert.That(post, Is.Not.Null, "POST checkouts endpoint should be mapped");
        var authorizeData = post!.Metadata.GetOrderedMetadata<IAuthorizeData>();
        Assert.That(authorizeData, Is.Not.Empty);
        Assert.That(authorizeData.Any(a => a.Policy == "IntegrationPaymentsCheckoutsWrite"), Is.True);
    }

    [Test]
    public void MapPaymentsIntegrationEndpoints_GetRequiresReadPolicy()
    {
        var endpoints = MapIntegrationRoutes();
        var get = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("integrations/payments/checkouts", System.StringComparison.Ordinal)
            && HasMethod(e, "GET"));

        Assert.That(get, Is.Not.Null, "GET checkouts endpoint should be mapped");
        var authorizeData = get!.Metadata.GetOrderedMetadata<IAuthorizeData>();
        Assert.That(authorizeData, Is.Not.Empty);
        Assert.That(authorizeData.Any(a => a.Policy == "IntegrationPaymentsCheckoutsRead"), Is.True);
    }
}
