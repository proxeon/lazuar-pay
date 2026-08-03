using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Lhdn.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class LhdnEndpointsAuthorizationTests
{
    private static (WebApplication App, System.Collections.Generic.List<RouteEndpoint> Endpoints) MapEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<global::BuildingBlocks.Application.IExecutionContextAccessor>());

        var app = builder.Build();
        app.MapLhdnEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        return (app, endpoints);
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

    private static string Dump(System.Collections.Generic.List<RouteEndpoint> endpoints) =>
        string.Join(", ", endpoints.Select(e =>
        {
            var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            return $"{string.Join('|', methods)} {e.RoutePattern.RawText}";
        }));

    [Test]
    public void MapLhdnEndpoints_DocumentWrite_Requires_IntegrationLhdnDocumentsWrite()
    {
        var (_, endpoints) = MapEndpoints();

        var postDocuments = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("documents", System.StringComparison.OrdinalIgnoreCase)
            && !raw.Contains("cancel", System.StringComparison.OrdinalIgnoreCase)
            && !raw.Contains("{", System.StringComparison.Ordinal)
            && HasMethod(e, "POST"));

        var cancel = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("cancel", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "POST"));

        Assert.That(postDocuments, Is.Not.Null, $"POST documents not found. Routes: {Dump(endpoints)}");
        Assert.That(cancel, Is.Not.Null, $"POST cancel not found. Routes: {Dump(endpoints)}");
        AssertPolicy(postDocuments, "IntegrationLhdnDocumentsWrite", "POST /lhdn/documents");
        AssertPolicy(cancel, "IntegrationLhdnDocumentsWrite", "POST /lhdn/documents/{id}/cancel");
    }

    [Test]
    public void MapLhdnEndpoints_DocumentRead_Requires_IntegrationLhdnDocumentsRead()
    {
        var (_, endpoints) = MapEndpoints();

        var getDocument = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("documents", System.StringComparison.OrdinalIgnoreCase)
            && !raw.Contains("cancel", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "GET"));

        Assert.That(getDocument, Is.Not.Null, $"GET document not found. Routes: {Dump(endpoints)}");
        AssertPolicy(getDocument, "IntegrationLhdnDocumentsRead", "GET /lhdn/documents/{internalId}");
    }

    [Test]
    public void MapLhdnEndpoints_ApiKeys_Require_OrgAdmin()
    {
        var (_, endpoints) = MapEndpoints();

        var listKeys = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("api-keys", System.StringComparison.OrdinalIgnoreCase)
            && !raw.Contains("{", System.StringComparison.Ordinal)
            && HasMethod(e, "GET"));

        var generateKeys = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("api-keys", System.StringComparison.OrdinalIgnoreCase)
            && !raw.Contains("{", System.StringComparison.Ordinal)
            && HasMethod(e, "POST"));

        var revokeKeys = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("api-keys", System.StringComparison.OrdinalIgnoreCase)
            && raw.Contains("{", System.StringComparison.Ordinal)
            && HasMethod(e, "DELETE"));

        Assert.That(listKeys, Is.Not.Null, $"GET api-keys not found. Routes: {Dump(endpoints)}");
        Assert.That(generateKeys, Is.Not.Null, $"POST api-keys not found. Routes: {Dump(endpoints)}");
        Assert.That(revokeKeys, Is.Not.Null, $"DELETE api-keys not found. Routes: {Dump(endpoints)}");
        AssertPolicy(listKeys, "OrgAdmin", "GET /lhdn/api-keys");
        AssertPolicy(generateKeys, "OrgAdmin", "POST /lhdn/api-keys");
        AssertPolicy(revokeKeys, "OrgAdmin", "DELETE /lhdn/api-keys/{id}");
    }

    [Test]
    public void MapLhdnEndpoints_TaxpayerValidate_Requires_IntegrationLhdnDocumentsRead()
    {
        var (_, endpoints) = MapEndpoints();

        var validate = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("taxpayer/validate", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "POST"));

        Assert.That(validate, Is.Not.Null, $"POST taxpayer/validate not found. Routes: {Dump(endpoints)}");
        AssertPolicy(validate, "IntegrationLhdnDocumentsRead", "POST /lhdn/taxpayer/validate");
    }

    [Test]
    public void MapLhdnEndpoints_TenantConfig_Requires_OrgAdmin()
    {
        var (_, endpoints) = MapEndpoints();

        var getConfig = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("lhdn-config", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "GET"));

        var putConfig = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw
            && raw.Contains("lhdn-config", System.StringComparison.OrdinalIgnoreCase)
            && HasMethod(e, "PUT"));

        Assert.That(getConfig, Is.Not.Null, $"GET lhdn-config not found. Routes: {Dump(endpoints)}");
        Assert.That(putConfig, Is.Not.Null, $"PUT lhdn-config not found. Routes: {Dump(endpoints)}");
        AssertPolicy(getConfig, "OrgAdmin", "GET /lhdn/workspaces/{id}/lhdn-config");
        AssertPolicy(putConfig, "OrgAdmin", "PUT /lhdn/workspaces/{id}/lhdn-config");
    }
}
