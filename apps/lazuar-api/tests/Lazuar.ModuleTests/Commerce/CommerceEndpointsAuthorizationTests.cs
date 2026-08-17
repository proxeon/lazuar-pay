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
        var adminGroup = app.MapGroup("/admin/commerce").RequireAuthorization("OrgRead");
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
    public void MapCommerceEndpoints_AnonymizeSubscriber_Requires_OrgAdmin()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<global::BuildingBlocks.Application.IExecutionContextAccessor>());
        builder.Services.AddSingleton(Substitute.For<Modules.Commerce.Application.Queries.ICommerceQueryService>());

        var app = builder.Build();
        var adminGroup = app.MapGroup("/admin/commerce").RequireAuthorization("OrgRead");
        adminGroup.MapSubscriberEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var anonymize = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("anonymize", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "POST"));

        Assert.That(anonymize, Is.Not.Null, "POST subscribers/{id}/anonymize not found.");
        AssertPolicy(anonymize, "OrgAdmin", "POST /admin/commerce/subscribers/{id}/anonymize");
    }

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

    [Test]
    public void MapCommerceEndpoints_ProductPost_Requires_OrgMember()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<global::BuildingBlocks.Application.IExecutionContextAccessor>());
        builder.Services.AddSingleton(Substitute.For<Modules.Commerce.Application.Queries.ICommerceQueryService>());
        var app = builder.Build();
        var adminGroup = app.MapGroup("/admin/commerce").RequireAuthorization("OrgRead");
        adminGroup.MapProductEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var post = endpoints.Single(e =>
            e.RoutePattern.RawText == "/admin/commerce/products" && HasMethod(e, "POST"));
        AssertPolicy(post, "OrgMember", "POST /admin/commerce/products");

        var get = endpoints.Single(e =>
            e.RoutePattern.RawText == "/admin/commerce/products" && HasMethod(e, "GET"));
        AssertPolicy(get, "OrgRead", "GET /admin/commerce/products");
    }

    [Test]
    public void MapCommerceEndpoints_Refund_Requires_OrgMember()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<global::BuildingBlocks.Application.IExecutionContextAccessor>());
        builder.Services.AddSingleton(Substitute.For<Modules.Commerce.Application.Queries.ICommerceQueryService>());
        var app = builder.Build();
        var adminGroup = app.MapGroup("/admin/commerce").RequireAuthorization("OrgRead");
        adminGroup.MapTransactionEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var refund = endpoints.Single(e =>
            e.RoutePattern.RawText!.Contains("refund", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "POST"));
        AssertPolicy(refund, "OrgMember", "POST /admin/commerce/transactions/{id}/refund");
    }

    [Test]
    public void MapCommerceEndpoints_GetSubscribers_Requires_OrgRead()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<global::BuildingBlocks.Application.IExecutionContextAccessor>());
        builder.Services.AddSingleton(Substitute.For<Modules.Commerce.Application.Queries.ICommerceQueryService>());
        var app = builder.Build();
        var adminGroup = app.MapGroup("/admin/commerce").RequireAuthorization("OrgRead");
        adminGroup.MapSubscriberEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var list = endpoints.Single(e =>
            e.RoutePattern.RawText == "/admin/commerce/subscribers" && HasMethod(e, "GET"));
        AssertPolicy(list, "OrgRead", "GET /admin/commerce/subscribers");
    }

    [Test]
    public void MapCommerceEndpoints_SubscriberWrites_Require_OrgMember()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<global::BuildingBlocks.Application.IExecutionContextAccessor>());
        builder.Services.AddSingleton(Substitute.For<Modules.Commerce.Application.Queries.ICommerceQueryService>());
        var app = builder.Build();
        var adminGroup = app.MapGroup("/admin/commerce").RequireAuthorization("OrgRead");
        adminGroup.MapSubscriberEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var changePlan = endpoints.Single(e =>
            e.RoutePattern.RawText!.Contains("change-plan", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "POST"));
        var quantity = endpoints.Single(e =>
            e.RoutePattern.RawText!.Contains("quantity", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "POST"));
        var pauseCollection = endpoints.Single(e =>
            e.RoutePattern.RawText!.Contains("collection/pause", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "POST"));
        var resumeCollection = endpoints.Single(e =>
            e.RoutePattern.RawText!.Contains("collection/resume", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "POST"));
        var export = endpoints.Single(e =>
            e.RoutePattern.RawText!.Contains("subscribers/export", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "GET"));

        AssertPolicy(changePlan, "OrgMember", "POST /admin/commerce/subscribers/{id}/change-plan");
        AssertPolicy(quantity, "OrgMember", "POST /admin/commerce/subscribers/{id}/quantity");
        AssertPolicy(pauseCollection, "OrgMember", "POST /admin/commerce/subscribers/{id}/collection/pause");
        AssertPolicy(resumeCollection, "OrgMember", "POST /admin/commerce/subscribers/{id}/collection/resume");
        AssertPolicy(export, "OrgMember", "GET /admin/commerce/subscribers/export");
    }
}
