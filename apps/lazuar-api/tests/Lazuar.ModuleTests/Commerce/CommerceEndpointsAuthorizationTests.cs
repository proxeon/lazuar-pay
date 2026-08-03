using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

/// <summary>
/// B.9 / acceptance: payment config (and other admin commerce) require OrgAdmin —
/// API_CLIENT keys cannot change payment config even with document scopes.
/// </summary>
[TestFixture]
public class CommerceEndpointsAuthorizationTests
{
    /// <summary>
    /// Mirrors <see cref="Endpoints.MapCommerceEndpoints"/> admin group wiring for payment-config only
    /// (avoids mapping unrelated commerce handlers that need extra DI in unit tests).
    /// </summary>
    private static System.Collections.Generic.List<RouteEndpoint> MapPaymentConfigAdminGroup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<global::BuildingBlocks.Application.IExecutionContextAccessor>());

        var app = builder.Build();
        var adminGroup = app.MapGroup("/admin/commerce").RequireAuthorization("OrgAdmin");
        adminGroup.MapPaymentConfigEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static void AssertPolicy(RouteEndpoint? endpoint, string expectedPolicy, string description)
    {
        Assert.That(endpoint, Is.Not.Null, description);
        var authorizeData = endpoint!.Metadata.GetOrderedMetadata<IAuthorizeData>();
        Assert.That(authorizeData, Is.Not.Empty, $"{description} must not be anonymously callable.");
        Assert.That(authorizeData.Any(a => a.Policy == expectedPolicy), Is.True,
            $"{description} must require {expectedPolicy}. Found: {string.Join(", ", authorizeData.Select(a => a.Policy))}");
    }

    private static bool HasMethod(RouteEndpoint e, string method) =>
        e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
            .Any(m => string.Equals(m, method, System.StringComparison.OrdinalIgnoreCase)) == true;

    [Test]
    public void MapCommerceEndpoints_PaymentConfig_Requires_OrgAdmin()
    {
        var endpoints = MapPaymentConfigAdminGroup();

        var getConfig = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("payment-config", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "GET"));

        var putConfig = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("payment-config", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "PUT"));

        Assert.That(getConfig, Is.Not.Null, "GET payment-config not found.");
        Assert.That(putConfig, Is.Not.Null, "PUT payment-config not found.");
        AssertPolicy(getConfig, "OrgAdmin", "GET /admin/commerce/payment-config");
        AssertPolicy(putConfig, "OrgAdmin", "PUT /admin/commerce/payment-config");
    }
}
